using System.Globalization;
using System.Text.RegularExpressions;
using Loyalty.Business.Services.Abstractions.Main;
using Loyalty.Business.Services.Abstractions.Storage.Azure;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using Tesseract;

namespace Loyalty.Business.Services.Implementations.Main
{
    public sealed class ReceiptValidationService : IReceiptValidationService
    {
        private const string ReceiptContainer = "receipts";
        private const int MaxOcrDimension = 1600; // Tesseract yan resolutionda ən sürətli — daha böyük > yavaş, daha kiçik > zəif tanıma.

        // Engine init bahalıdır (~1-3s + ~30MB RAM per dil). Singleton ilə hər request reuse edilir.
        // Tesseract.NET-də Engine thread-safe DEYİL — hər istifadə üçün lock + cache.
        private static readonly Lazy<TesseractEngine> _engine = new(() =>
        {
            var path = Path.Combine(AppContext.BaseDirectory, "tessdata");
            return new TesseractEngine(path, "aze+eng", EngineMode.LstmOnly);
        }, LazyThreadSafetyMode.ExecutionAndPublication);
        private static readonly object _engineLock = new();

        // Çekin cəmi sətrini tutmaq üçün — "Cəmi: 123,45" / "Total 67.50" / "Sum 12.00" / "Итого 99,99"
        private static readonly Regex AmountRegex = new(
            @"(?ix)
              (?:c[əə]mi|total|m[əə]bl[əə][ğq]|sum|итого|cemi)
              \s*[:\-]?\s*
              (?<amt>\d{1,5}[.,]\d{2})
              | (?<amt2>\d{1,5}[.,]\d{2})",
            RegexOptions.Compiled);

        private readonly IAzureStorage _storage;
        private readonly ILogger<ReceiptValidationService> _logger;

        public ReceiptValidationService(
            IAzureStorage storage,
            ILogger<ReceiptValidationService> logger)
        {
            _storage = storage;
            _logger = logger;
        }

        public async Task<ReceiptValidationResult> ValidateAsync(
            IFormFile image,
            decimal enteredAmount,
            decimal toleranceAzn,
            CancellationToken ct = default)
        {
            if (image == null || image.Length == 0)
                return new(false, null, null, "Çekin şəkli yüklənməyib.");

            if (image.Length > 5 * 1024 * 1024)
                return new(false, null, null, "Çekin şəkli 5 MB-dan böyükdür.");

            byte[] rawBytes;
            using (var ms = new MemoryStream())
            {
                await image.CopyToAsync(ms, ct);
                rawBytes = ms.ToArray();
            }

            var result = await RunOcrAndMatchAsync(rawBytes, enteredAmount, toleranceAzn, null, ct);

            // Yalnız uğurlu (və ya extract olunan) hallarda blob upload — uğursuz OCR-də spam yox.
            if (result.ExtractedAmount.HasValue)
            {
                string? url = null;
                try
                {
                    var uploaded = await UploadReceiptOnlyAsync(image, ct);
                    url = uploaded.url;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Receipt blob upload failed");
                }
                return result with { ReceiptUrl = url };
            }
            return result;
        }

        public async Task<ReceiptValidationResult> ValidateBytesAsync(
            byte[] imageBytes,
            decimal enteredAmount,
            decimal toleranceAzn,
            string? existingReceiptUrl,
            CancellationToken ct = default)
        {
            if (imageBytes == null || imageBytes.Length == 0)
                return new(false, null, existingReceiptUrl, "Çek bytes boşdur.");
            return await RunOcrAndMatchAsync(imageBytes, enteredAmount, toleranceAzn, existingReceiptUrl, ct);
        }

        public async Task<(string fileName, string url)> UploadReceiptOnlyAsync(IFormFile image, CancellationToken ct = default)
        {
            var wrapped = new FormFileCollection { image };
            var uploaded = await _storage.UploadAsync(ReceiptContainer, wrapped);
            var first = uploaded.FirstOrDefault();
            return (first.fileName, first.pathOrContainerName);
        }

        private async Task<ReceiptValidationResult> RunOcrAndMatchAsync(
            byte[] rawBytes,
            decimal enteredAmount,
            decimal toleranceAzn,
            string? carriedReceiptUrl,
            CancellationToken ct)
        {
            // Tesseract native binary və tessdata mövcudluğunu əvvəldən yoxla — sonradan opaque crash yox.
            var tessdataPath = Path.Combine(AppContext.BaseDirectory, "tessdata");
            if (!Directory.Exists(tessdataPath))
            {
                _logger.LogError("tessdata directory missing at {Path}", tessdataPath);
                return new(false, null, carriedReceiptUrl, "OCR data faylları tapılmadı (server konfiqurasiya xətası).");
            }
            var azeData = Path.Combine(tessdataPath, "aze.traineddata");
            var engData = Path.Combine(tessdataPath, "eng.traineddata");
            if (!System.IO.File.Exists(azeData) || !System.IO.File.Exists(engData))
            {
                _logger.LogError("tessdata lang files missing. aze={Aze} eng={Eng}",
                    System.IO.File.Exists(azeData), System.IO.File.Exists(engData));
                return new(false, null, carriedReceiptUrl, "OCR dil faylları tapılmadı (aze/eng).");
            }

            byte[] preprocessed;
            try
            {
                preprocessed = await PreprocessAsync(rawBytes, ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Receipt image preprocessing failed. RawSize={Size}", rawBytes.Length);
                return new(false, null, carriedReceiptUrl, "Çek şəkli oxunmadı (format dəstəklənmir və ya korlanıb).");
            }

            string ocrText;
            try
            {
                ocrText = RunOcr(preprocessed, tessdataPath);
            }
            catch (DllNotFoundException ex)
            {
                _logger.LogError(ex, "Tesseract native library missing (DllNotFound). OS={OS}",
                    System.Runtime.InteropServices.RuntimeInformation.OSDescription);
                return new(false, null, carriedReceiptUrl,
                    "OCR mühərriki serverdə qurulmayıb. Docker image-də tesseract-ocr paketinin olduğundan əmin olun.");
            }
            catch (TypeInitializationException ex)
            {
                _logger.LogError(ex, "Tesseract engine type init failed (often P/Invoke arch mismatch).");
                return new(false, null, carriedReceiptUrl,
                    "OCR mühərriki başlana bilmədi (arch uyğunsuzluğu və ya tessdata problemi).");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Tesseract OCR call failed. ExType={Type}, Msg={Msg}",
                    ex.GetType().Name, ex.Message);
                return new(false, null, carriedReceiptUrl, $"OCR uğursuz oldu: {ex.Message}");
            }

            _logger.LogInformation("OCR completed. TextLen={Len}, Preview={Preview}",
                ocrText.Length,
                ocrText.Length > 0 ? ocrText.Substring(0, Math.Min(120, ocrText.Length)).Replace('\n', ' ') : "<empty>");

            var extracted = ExtractAmount(ocrText);
            if (extracted is null)
            {
                _logger.LogInformation("OCR found no amount in receipt text.");
                return new(false, null, carriedReceiptUrl, "Çekdə məbləğ tapılmadı. Yenidən aydın şəkil çəkin.");
            }

            var entered = Math.Abs(enteredAmount);
            var tol = toleranceAzn <= 0 ? 1m : toleranceAzn;
            var diff = Math.Abs(extracted.Value - entered);

            if (diff > tol)
            {
                _logger.LogWarning(
                    "OCR amount mismatch. Entered={Entered}, Ocr={Ocr}, Tolerance={Tol}, Diff={Diff}",
                    entered, extracted, tol, diff);
                return new(false, extracted, carriedReceiptUrl,
                    $"Çek məbləği ({extracted:0.##}) daxil edilən məbləğlə ({entered:0.##}) uyğun deyil.");
            }

            return new(true, extracted, carriedReceiptUrl, null);
        }

        private static async Task<byte[]> PreprocessAsync(byte[] input, CancellationToken ct)
        {
            // ImageSharp avtomatik format detect (jpeg/png/webp). L8 = 8-bit grayscale.
            using var image = Image.Load<L8>(input);

            // Çek şəkilləri smartfonlardan adətən 3000-4000px gəlir — bu Tesseract üçün çox böyükdür və yavaşladır.
            // 1600px sərhədinə qədər kiçilt; əgər orijinal daha kiçikdirsə toxunma.
            var maxDim = Math.Max(image.Width, image.Height);
            if (maxDim > MaxOcrDimension)
            {
                var scale = (float)MaxOcrDimension / maxDim;
                var newW = (int)(image.Width * scale);
                var newH = (int)(image.Height * scale);
                image.Mutate(x => x.Resize(newW, newH));
            }
            image.Mutate(x => x.Contrast(1.4f));

            using var outMs = new MemoryStream();
            await image.SaveAsPngAsync(outMs, ct);
            return outMs.ToArray();
        }

        private static string RunOcr(byte[] imageBytes, string tessdataPath)
        {
            // Engine singleton — init bir dəfə. Engine thread-safe DEYİL → lock.
            lock (_engineLock)
            {
                var engine = _engine.Value;
                engine.DefaultPageSegMode = PageSegMode.SingleBlock; // PSM 6 — çek üçün tək blok mətn.
                using var pix = Pix.LoadFromMemory(imageBytes);
                using var page = engine.Process(pix);
                return page.GetText() ?? string.Empty;
            }
        }

        internal static decimal? ExtractAmount(string ocrText)
        {
            if (string.IsNullOrWhiteSpace(ocrText)) return null;

            decimal? best = null;
            foreach (Match m in AmountRegex.Matches(ocrText))
            {
                var raw = m.Groups["amt"].Success ? m.Groups["amt"].Value
                        : m.Groups["amt2"].Success ? m.Groups["amt2"].Value
                        : null;
                if (raw is null) continue;

                raw = raw.Replace(',', '.');
                if (!decimal.TryParse(raw, NumberStyles.Number, CultureInfo.InvariantCulture, out var value))
                    continue;

                // Cəmi adətən çekdəki ən böyük rəqəmdir
                if (best is null || value > best) best = value;
            }
            return best;
        }

    }
}

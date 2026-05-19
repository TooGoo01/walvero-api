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

            byte[] preprocessed;
            try
            {
                preprocessed = await PreprocessAsync(rawBytes, ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Receipt image preprocessing failed");
                return new(false, null, null, "Çek şəkli oxunmadı.");
            }

            string ocrText;
            try
            {
                ocrText = RunOcr(preprocessed);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Tesseract OCR call failed");
                return new(false, null, null, "OCR uğursuz oldu.");
            }

            var extracted = ExtractAmount(ocrText);
            if (extracted is null)
            {
                _logger.LogInformation("OCR found no amount in receipt text. TextLen={Len}", ocrText.Length);
                return new(false, null, null, "Çekdə məbləğ tapılmadı.");
            }

            var entered = Math.Abs(enteredAmount);
            var tol = toleranceAzn <= 0 ? 1m : toleranceAzn;
            var diff = Math.Abs(extracted.Value - entered);

            // Yükləməni OCR-dən sonra et — uğursuz OCR halında lazımsız blob yaratma
            string? receiptUrl;
            try
            {
                receiptUrl = await UploadAsync(image, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Receipt blob upload failed");
                return new(false, extracted, null, "Çek faylının yüklənməsi alınmadı.");
            }

            if (diff > tol)
            {
                _logger.LogWarning(
                    "OCR amount mismatch. Entered={Entered}, Ocr={Ocr}, Tolerance={Tol}, Diff={Diff}",
                    entered, extracted, tol, diff);
                return new(false, extracted, receiptUrl,
                    $"Çek məbləği ({extracted:0.##}) daxil edilən məbləğlə ({entered:0.##}) uyğun deyil.");
            }

            return new(true, extracted, receiptUrl, null);
        }

        private static async Task<byte[]> PreprocessAsync(byte[] input, CancellationToken ct)
        {
            using var image = Image.Load<L8>(input);
            image.Mutate(x =>
                x.Resize(image.Width * 2, image.Height * 2)
                 .Contrast(1.4f));
            using var outMs = new MemoryStream();
            await image.SaveAsPngAsync(outMs, ct);
            return outMs.ToArray();
        }

        private static string RunOcr(byte[] imageBytes)
        {
            var tessdataPath = Path.Combine(AppContext.BaseDirectory, "tessdata");
            using var engine = new TesseractEngine(tessdataPath, "aze+eng", EngineMode.Default);
            using var pix = Pix.LoadFromMemory(imageBytes);
            using var page = engine.Process(pix);
            return page.GetText() ?? string.Empty;
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

        private async Task<string?> UploadAsync(IFormFile image, CancellationToken ct)
        {
            // IFormFileCollection lazımdır — wrap into a single-item list
            var wrapped = new FormFileCollection { image };
            var uploaded = await _storage.UploadAsync(ReceiptContainer, wrapped);
            return uploaded.FirstOrDefault().pathOrContainerName;
        }
    }
}

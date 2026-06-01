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
using ZXing;
using ZXing.ImageSharp;

namespace Loyalty.Business.Services.Implementations.Main
{
    public sealed class ReceiptValidationService : IReceiptValidationService
    {
        private const string ReceiptContainer = "receipts";
        private const int MaxOcrDimension = 1600;

        private static readonly Lazy<TesseractEngine> _engine = new(() =>
        {
            var path = Path.Combine(AppContext.BaseDirectory, "tessdata");
            return new TesseractEngine(path, "aze+eng", EngineMode.LstmOnly);
        }, LazyThreadSafetyMode.ExecutionAndPublication);
        private static readonly object _engineLock = new();

        private static readonly Regex EkassaUrlRegex = new(
            @"monitoring\.e-kassa\.gov\.az/.*?[?&#]doc=(?<doc>[A-Za-z0-9_\-]+)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex DateRegex = new(
            @"\b(?<d>\d{1,2})[./\-](?<m>\d{1,2})[./\-](?<y>\d{2,4})\b",
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
            ReceiptValidationContext ctx,
            CancellationToken ct = default)
        {
            if (image == null || image.Length == 0)
                return new(false, null, null, "Çekin şəkli yüklənməyib.", FailReasonCode: "MissingImage");

            if (image.Length > 5 * 1024 * 1024)
                return new(false, null, null, "Çekin şəkli 5 MB-dan böyükdür.", FailReasonCode: "ImageTooLarge");

            byte[] rawBytes;
            using (var ms = new MemoryStream())
            {
                await image.CopyToAsync(ms, ct);
                rawBytes = ms.ToArray();
            }

            var result = await ProcessBytesAsync(rawBytes, ctx, null, ct);

            if (result.ExtractedAmount.HasValue || result.QrVerified || !string.IsNullOrEmpty(result.QrDocId))
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
            ReceiptValidationContext ctx,
            string? existingReceiptUrl,
            CancellationToken ct = default)
        {
            if (imageBytes == null || imageBytes.Length == 0)
                return new(false, null, existingReceiptUrl, "Çek bytes boşdur.", FailReasonCode: "MissingImage");
            return await ProcessBytesAsync(imageBytes, ctx, existingReceiptUrl, ct);
        }

        public async Task<(string fileName, string url)> UploadReceiptOnlyAsync(IFormFile image, CancellationToken ct = default)
        {
            var wrapped = new FormFileCollection { image };
            var uploaded = await _storage.UploadAsync(ReceiptContainer, wrapped);
            var first = uploaded.FirstOrDefault();
            return (first.fileName, first.pathOrContainerName);
        }

        private async Task<ReceiptValidationResult> ProcessBytesAsync(
            byte[] rawBytes,
            ReceiptValidationContext ctx,
            string? carriedReceiptUrl,
            CancellationToken ct)
        {
            string? qrDocId = ctx.QrDocIdFromScanner;
            decimal? extracted = null;
            DateTime? receiptDate = null;
            string? objectCode = null;
            string? physicalReceiptId = null;
            bool qrVerified = false;

            // Step 1A: scanner-side e-kassa JSON capture əla isə həmin dəyərləri istifadə et.
            var payload = ctx.QrVerifiedPayload;
            if (payload != null && payload.Amount.HasValue)
            {
                qrVerified = true;
                qrDocId ??= payload.DocId;
                extracted = payload.Amount;
                receiptDate = payload.CreatedAtUtc;
                objectCode = payload.CashBoxObjectCode;
                physicalReceiptId = !string.IsNullOrWhiteSpace(payload.DocumentNumber)
                    ? payload.DocumentNumber
                    : payload.DocId;
            }

            // Step 1B: server-side ZXing QR decode (scanner uğursuz olsa belə backup).
            if (string.IsNullOrEmpty(qrDocId))
            {
                try
                {
                    qrDocId = TryDecodeQr(rawBytes);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "ZXing QR decode failed");
                }
            }
            // ZXing tapsa amma DocumentNumber yoxsa fallback dedupe key olaraq doc-id istifadə.
            if (string.IsNullOrWhiteSpace(physicalReceiptId) && !string.IsNullOrWhiteSpace(qrDocId))
                physicalReceiptId = qrDocId;

            // Step 2: OCR fallback — payload yoxdursa amount + date + object-code + receipt-id parse.
            if (!qrVerified)
            {
                var tessdataPath = Path.Combine(AppContext.BaseDirectory, "tessdata");
                if (!Directory.Exists(tessdataPath))
                {
                    _logger.LogError("tessdata directory missing at {Path}", tessdataPath);
                    return new(false, null, carriedReceiptUrl,
                        "OCR data faylları tapılmadı (server konfiqurasiya xətası).",
                        QrDocId: qrDocId, PhysicalReceiptId: physicalReceiptId,
                        FailReasonCode: "OcrUnreadable");
                }
                var azeData = Path.Combine(tessdataPath, "aze.traineddata");
                var engData = Path.Combine(tessdataPath, "eng.traineddata");
                if (!System.IO.File.Exists(azeData) || !System.IO.File.Exists(engData))
                {
                    _logger.LogError("tessdata lang files missing.");
                    return new(false, null, carriedReceiptUrl,
                        "OCR dil faylları tapılmadı (aze/eng).",
                        QrDocId: qrDocId, PhysicalReceiptId: physicalReceiptId,
                        FailReasonCode: "OcrUnreadable");
                }

                byte[] preprocessed;
                try
                {
                    preprocessed = await PreprocessAsync(rawBytes, ct);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Receipt image preprocessing failed. RawSize={Size}", rawBytes.Length);
                    return new(false, null, carriedReceiptUrl,
                        "Çek şəkli oxunmadı (format dəstəklənmir və ya korlanıb).",
                        QrDocId: qrDocId, PhysicalReceiptId: physicalReceiptId,
                        FailReasonCode: "OcrUnreadable");
                }

                string ocrText;
                try
                {
                    ocrText = RunOcr(preprocessed);
                }
                catch (DllNotFoundException ex)
                {
                    _logger.LogError(ex, "Tesseract native library missing.");
                    return new(false, null, carriedReceiptUrl,
                        "OCR mühərriki serverdə qurulmayıb.",
                        QrDocId: qrDocId, PhysicalReceiptId: physicalReceiptId,
                        FailReasonCode: "OcrUnreadable");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Tesseract OCR call failed.");
                    return new(false, null, carriedReceiptUrl,
                        $"OCR uğursuz oldu: {ex.Message}",
                        QrDocId: qrDocId, PhysicalReceiptId: physicalReceiptId,
                        FailReasonCode: "OcrUnreadable");
                }

                _logger.LogInformation("OCR completed. TextLen={Len}", ocrText.Length);

                extracted = ExtractByLabel(ocrText, ctx.OcrLabelAmount, AmountValuePattern)
                            ?? ExtractFallbackAmount(ocrText);
                objectCode ??= ExtractByLabelString(ocrText, ctx.OcrLabelObjectCode, ObjectCodeValuePattern);
                if (string.IsNullOrWhiteSpace(physicalReceiptId))
                    physicalReceiptId = ExtractByLabelString(ocrText, ctx.OcrLabelReceiptId, ReceiptIdValuePattern);
                receiptDate ??= ExtractDate(ocrText);

                if (extracted is null)
                {
                    _logger.LogInformation("OCR found no amount in receipt text.");
                    return new(false, null, carriedReceiptUrl,
                        "Çekdə məbləğ tapılmadı. Yenidən aydın şəkil çəkin.",
                        QrDocId: qrDocId, PhysicalReceiptId: physicalReceiptId,
                        ReceiptDate: receiptDate, ExtractedObjectCode: objectCode,
                        FailReasonCode: "OcrUnreadable");
                }
            }

            // Step 3: compare (priority — RedeemService / ReceiptOcrJob FailReasonCode-a əsasən IsSuspicious yazır).

            // 3a. Date match (yalnız date varsa yoxla)
            if (receiptDate.HasValue && !IsTodayInTenantZone(receiptDate.Value, ctx.TimeZoneId))
            {
                return new(false, extracted, carriedReceiptUrl,
                    $"Çekin tarixi bu günkü tarixə uyğun deyil ({receiptDate:dd.MM.yyyy}).",
                    QrDocId: qrDocId, PhysicalReceiptId: physicalReceiptId,
                    ReceiptDate: receiptDate, ExtractedObjectCode: objectCode,
                    QrVerified: qrVerified,
                    FailReasonCode: "ReceiptDateMismatch");
            }

            // 3b. Object code match (branch-da kod təyin edilibsə və receipt-də tapılsa)
            if (!string.IsNullOrWhiteSpace(ctx.BranchObjectCode)
                && !string.IsNullOrWhiteSpace(objectCode)
                && !string.Equals(ctx.BranchObjectCode.Trim(), objectCode.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                return new(false, extracted, carriedReceiptUrl,
                    $"Obyekt Kodu uyğun deyil (gözlənilən {ctx.BranchObjectCode}, tapıldı {objectCode}).",
                    QrDocId: qrDocId, PhysicalReceiptId: physicalReceiptId,
                    ReceiptDate: receiptDate, ExtractedObjectCode: objectCode,
                    QrVerified: qrVerified,
                    FailReasonCode: "ObjectCodeMismatch");
            }

            // 3c. QR missing strict mode
            if (ctx.RequireQrVerification && !qrVerified)
            {
                return new(false, extracted, carriedReceiptUrl,
                    "QR doğrulaması tələb olunur — çekin QR kodu oxunmadı.",
                    QrDocId: qrDocId, PhysicalReceiptId: physicalReceiptId,
                    ReceiptDate: receiptDate, ExtractedObjectCode: objectCode,
                    QrVerified: qrVerified,
                    FailReasonCode: "QrMissing");
            }

            // 3d. Amount tolerance
            var entered = Math.Abs(ctx.EnteredAmount);
            var tol = ctx.ToleranceAzn <= 0 ? 1m : ctx.ToleranceAzn;
            var diff = Math.Abs((extracted ?? 0m) - entered);
            if (extracted.HasValue && diff > tol)
            {
                _logger.LogWarning("OCR amount mismatch. Entered={Entered}, Ocr={Ocr}, Tolerance={Tol}, Diff={Diff}",
                    entered, extracted, tol, diff);
                return new(false, extracted, carriedReceiptUrl,
                    $"Çek məbləği ({extracted:0.##}) daxil edilən məbləğlə ({entered:0.##}) uyğun deyil.",
                    QrDocId: qrDocId, PhysicalReceiptId: physicalReceiptId,
                    ReceiptDate: receiptDate, ExtractedObjectCode: objectCode,
                    QrVerified: qrVerified,
                    FailReasonCode: "OcrMismatch");
            }

            return new(true, extracted, carriedReceiptUrl, null,
                QrDocId: qrDocId, PhysicalReceiptId: physicalReceiptId,
                ReceiptDate: receiptDate, ExtractedObjectCode: objectCode,
                QrVerified: qrVerified);
        }

        private static async Task<byte[]> PreprocessAsync(byte[] input, CancellationToken ct)
        {
            using var image = Image.Load<L8>(input);
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

        private static string RunOcr(byte[] imageBytes)
        {
            lock (_engineLock)
            {
                var engine = _engine.Value;
                engine.DefaultPageSegMode = PageSegMode.SingleBlock;
                using var pix = Pix.LoadFromMemory(imageBytes);
                using var page = engine.Process(pix);
                return page.GetText() ?? string.Empty;
            }
        }

        private static string? TryDecodeQr(byte[] imageBytes)
        {
            using var rgba = Image.Load<Rgba32>(imageBytes);
            var reader = new ZXing.ImageSharp.BarcodeReader<Rgba32>
            {
                AutoRotate = true,
                Options = new ZXing.Common.DecodingOptions
                {
                    TryHarder = true,
                    PossibleFormats = new[] { BarcodeFormat.QR_CODE }
                }
            };
            var result = reader.Decode(rgba);
            if (result == null || string.IsNullOrWhiteSpace(result.Text)) return null;
            var m = EkassaUrlRegex.Match(result.Text);
            return m.Success ? m.Groups["doc"].Value : null;
        }

        // Dynamic label-əsaslı parse: "<label> ... 12,34" formasında məbləğ.
        private const string AmountValuePattern = @"\s*[:\-]?\s*(?<val>\d{1,5}[.,]\d{2})";
        private const string ObjectCodeValuePattern = @"\s*[:\-]?\s*(?<val>[A-Za-z0-9]{3,20})";
        private const string ReceiptIdValuePattern = @"\s*[:\-]?\s*(?<val>[A-Za-z0-9\-]{5,40})";

        private static decimal? ExtractByLabel(string ocrText, string label, string valuePattern)
        {
            if (string.IsNullOrWhiteSpace(ocrText) || string.IsNullOrWhiteSpace(label)) return null;
            var pat = Regex.Escape(label) + valuePattern;
            var m = Regex.Match(ocrText, pat, RegexOptions.IgnoreCase);
            if (!m.Success) return null;
            var raw = m.Groups["val"].Value.Replace(',', '.');
            return decimal.TryParse(raw, NumberStyles.Number, CultureInfo.InvariantCulture, out var v) ? v : null;
        }

        private static string? ExtractByLabelString(string ocrText, string label, string valuePattern)
        {
            if (string.IsNullOrWhiteSpace(ocrText) || string.IsNullOrWhiteSpace(label)) return null;
            var pat = Regex.Escape(label) + valuePattern;
            var m = Regex.Match(ocrText, pat, RegexOptions.IgnoreCase);
            return m.Success ? m.Groups["val"].Value : null;
        }

        // Label tapılmadıqda OCR-da ən böyük decimal-ı götür (köhnə davranış).
        private static decimal? ExtractFallbackAmount(string ocrText)
        {
            if (string.IsNullOrWhiteSpace(ocrText)) return null;
            decimal? best = null;
            foreach (Match m in Regex.Matches(ocrText, @"\d{1,5}[.,]\d{2}"))
            {
                var raw = m.Value.Replace(',', '.');
                if (!decimal.TryParse(raw, NumberStyles.Number, CultureInfo.InvariantCulture, out var v)) continue;
                if (best is null || v > best) best = v;
            }
            return best;
        }

        private static DateTime? ExtractDate(string ocrText)
        {
            if (string.IsNullOrWhiteSpace(ocrText)) return null;
            foreach (Match m in DateRegex.Matches(ocrText))
            {
                if (!int.TryParse(m.Groups["d"].Value, out var d)) continue;
                if (!int.TryParse(m.Groups["m"].Value, out var mo)) continue;
                if (!int.TryParse(m.Groups["y"].Value, out var y)) continue;
                if (y < 100) y += 2000;
                if (d is < 1 or > 31 || mo is < 1 or > 12 || y < 2000 || y > 2100) continue;
                try { return new DateTime(y, mo, d, 12, 0, 0, DateTimeKind.Utc); }
                catch { /* invalid combo (məs 31.02), atla */ }
            }
            return null;
        }

        private static bool IsTodayInTenantZone(DateTime receiptDate, string timeZoneId)
        {
            try
            {
                var tz = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
                var todayLocal = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz).Date;
                var receiptUtc = receiptDate.Kind == DateTimeKind.Utc
                    ? receiptDate
                    : DateTime.SpecifyKind(receiptDate, DateTimeKind.Utc);
                var receiptLocal = TimeZoneInfo.ConvertTimeFromUtc(receiptUtc, tz).Date;
                return todayLocal == receiptLocal;
            }
            catch
            {
                // TZ tapılmadısa UTC fallback
                return receiptDate.Date == DateTime.UtcNow.Date;
            }
        }
    }
}

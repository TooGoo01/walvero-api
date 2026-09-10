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
        private const int MaxOcrDimension = 2200; // telefon fotosunda kiçik rəqəmlər 1600-də əriyirdi
        private const int MinOcrDimension = 1400; // kiçik/uzaqdan çəkilmiş şəkilləri upscale et — LSTM kiçik mətni tutmur

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

        // Fiskal çek əlamətləri — bunlardan biri OCR mətnində varsa çek fiskal sayılır.
        private static readonly Regex FiscalMarkerRegex = new(
            @"e-?kassa|fiskal|NMQ|NKA|VÖEN|VOEN",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        // Qeyri-fiskal slip-də dedupe üçün "Sifariş № 264" / "Sifariş #265" formaları.
        private static readonly Regex OrderNoRegex = new(
            @"Sifari[şs]\s*(?:№|#|N[oº]?)?\s*(?<val>\d{1,10})",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private readonly IAzureStorage _storage;
        private readonly IEkassaImageFetcher _ekassaFetcher;
        private readonly IReceiptVisionOcr _vision;
        private readonly ILogger<ReceiptValidationService> _logger;

        public ReceiptValidationService(
            IAzureStorage storage,
            IEkassaImageFetcher ekassaFetcher,
            IReceiptVisionOcr vision,
            ILogger<ReceiptValidationService> logger)
        {
            _storage = storage;
            _ekassaFetcher = ekassaFetcher;
            _vision = vision;
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
            string? taxNumber = null;
            string? physicalReceiptId = null;
            bool qrVerified = false;
            bool visionFiscal = false;      // vision OCR: fiskal əlaməti
            string? visionOrderNo = null;   // vision OCR: sifariş №

            // Step 1A: Flutter-in fetch etdiyi e-kassa image priority. AZ ISP-dən gəlir, ən etibarlı.
            byte[] ocrBytes = rawBytes;
            if (ctx.EkassaImageBytes != null && ctx.EkassaImageBytes.Length > 0)
            {
                ocrBytes = ctx.EkassaImageBytes;
                qrVerified = true;
                _logger.LogInformation("Ekassa image from Flutter (preferred). len={Len}",
                    ctx.EkassaImageBytes.Length);
            }
            else
            {
                // Flutter göndərmədi (köhnə APK ya QR yoxdur) — server-side ZXing decode cəhd.
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

                // Backend fallback fetch (Azure DC-də çox güman fail — datacenter IP block).
                if (!string.IsNullOrWhiteSpace(qrDocId))
                {
                    try
                    {
                        var fetched = await _ekassaFetcher.FetchAsync(qrDocId, ctx.EkassaUrlTemplate, ct);
                        if (fetched != null && fetched.Bytes.Length > 0)
                        {
                            ocrBytes = fetched.Bytes;
                            qrVerified = true;
                            _logger.LogInformation("Ekassa image backend-fetched. ct={Ct} len={Len}",
                                fetched.ContentType, fetched.Bytes.Length);
                        }
                        else
                        {
                            _logger.LogWarning("Ekassa fetch returned null — fallback to user photo");
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Ekassa fetch error — fallback to user photo");
                    }
                }
            }

            if (!string.IsNullOrWhiteSpace(qrDocId))
                physicalReceiptId = qrDocId;

            // Step 2: OCR — payload trust yoxdur, OCR həmişə işləyir (qrVerified halında e-kassa image-ə).
            string ocrText = string.Empty;
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
                    preprocessed = await PreprocessAsync(ocrBytes, ct);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Receipt image preprocessing failed. RawSize={Size}", ocrBytes.Length);
                    return new(false, null, carriedReceiptUrl,
                        "Çek şəkli oxunmadı (format dəstəklənmir və ya korlanıb).",
                        QrDocId: qrDocId, PhysicalReceiptId: physicalReceiptId,
                        FailReasonCode: "OcrUnreadable");
                }

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
                taxNumber ??= ExtractByLabelString(ocrText, ctx.OcrLabelTaxNumber, TaxNumberValuePattern);
                if (string.IsNullOrWhiteSpace(physicalReceiptId))
                    physicalReceiptId = ExtractByLabelString(ocrText, ctx.OcrLabelReceiptId, ReceiptIdValuePattern);
                receiptDate ??= ExtractDate(ocrText);

                // Vision OCR (Azure OpenAI) — Tesseract çek fotolarında məbləği səhv oxuyur.
                // Məbləği authoritative vision-dan götür; digər boş sahələri doldur.
                // Vision uğursuzsa Tesseract nəticəsi qalır (graceful degrade).
                try
                {
                    var v = await _vision.ReadAsync(ocrBytes, ct);
                    if (v.Ok)
                    {
                        if (v.Amount.HasValue) extracted = v.Amount;
                        objectCode ??= v.ObjectCode;
                        taxNumber ??= v.TaxNumber;
                        if (string.IsNullOrWhiteSpace(physicalReceiptId)) physicalReceiptId = v.ReceiptId;
                        receiptDate ??= v.Date;
                        visionFiscal = v.IsFiscal;
                        visionOrderNo = v.OrderNo;
                        _logger.LogInformation("Vision OCR ok. Amount={Amount} Fiscal={F}", v.Amount, v.IsFiscal);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Vision OCR failed; falling back to Tesseract result.");
                }

                if (extracted is null)
                {
                    _logger.LogInformation("OCR found no amount in receipt text.");
                    return new(false, null, carriedReceiptUrl,
                        "Çekdə məbləğ tapılmadı. Yenidən aydın şəkil çəkin.",
                        QrDocId: qrDocId, PhysicalReceiptId: physicalReceiptId,
                        ReceiptDate: receiptDate, ExtractedObjectCode: objectCode,
                        FailReasonCode: "OcrUnreadable",
                        ExtractedTaxNumber: taxNumber);
                }
            }

            // Fiskal-lıq detektoru: qeyri-fiskal "Ödəniş çeki"ndə bu əlamətlərin heç biri olmur.
            // AllowNonFiscalReceipts aktivdirsə belə çeklər QR tələbindən keçir, əvəzində
            // filial adı yoxlanır (3c'). Fiskal görünüşlü, amma QR oxunmayan çek yenə reject olunur.
            bool looksFiscal = qrVerified
                || !string.IsNullOrWhiteSpace(qrDocId)
                || !string.IsNullOrWhiteSpace(taxNumber)
                || !string.IsNullOrWhiteSpace(physicalReceiptId)
                || visionFiscal
                || FiscalMarkerRegex.IsMatch(ocrText);
            bool nonFiscalMode = ctx.AllowNonFiscalReceipts && !looksFiscal;

            if (nonFiscalMode)
            {
                _logger.LogInformation("Non-fiscal receipt mode. Tenant={TenantId} Branch={BranchId}",
                    ctx.TenantId, ctx.BranchId);

                // Dedupe fallback: Fiskal ID yoxdur — Sifariş № + tarix + filial kombinasiyası açar olur.
                if (string.IsNullOrWhiteSpace(physicalReceiptId))
                {
                    var orderMatch = OrderNoRegex.Match(ocrText);
                    var orderVal = orderMatch.Success ? orderMatch.Groups["val"].Value : visionOrderNo;
                    if (!string.IsNullOrWhiteSpace(orderVal))
                    {
                        var dedupeDate = receiptDate ?? TodayInTenantZone(ctx.TimeZoneId);
                        physicalReceiptId = $"NF:{ctx.BranchId ?? 0}:{dedupeDate:yyyyMMdd}:{orderVal}";
                    }
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
                    FailReasonCode: "ReceiptDateMismatch",
                    ExtractedTaxNumber: taxNumber,
                    IsNonFiscal: nonFiscalMode);
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
                    FailReasonCode: "ObjectCodeMismatch",
                    ExtractedTaxNumber: taxNumber);
            }

            // 3b'. Tax number (VÖEN) match — yeni
            if (!string.IsNullOrWhiteSpace(ctx.ExpectedTaxNumber)
                && !string.IsNullOrWhiteSpace(taxNumber)
                && !string.Equals(ctx.ExpectedTaxNumber.Trim(), taxNumber.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                return new(false, extracted, carriedReceiptUrl,
                    $"VÖEN uyğun deyil (gözlənilən {ctx.ExpectedTaxNumber}, tapıldı {taxNumber}).",
                    QrDocId: qrDocId, PhysicalReceiptId: physicalReceiptId,
                    ReceiptDate: receiptDate, ExtractedObjectCode: objectCode,
                    QrVerified: qrVerified,
                    FailReasonCode: "TaxNumberMismatch",
                    ExtractedTaxNumber: taxNumber);
            }

            // 3c. QR missing strict mode (qeyri-fiskal rejim QR tələbindən azaddır)
            if (ctx.RequireQrVerification && !qrVerified && !nonFiscalMode)
            {
                return new(false, extracted, carriedReceiptUrl,
                    "QR doğrulaması tələb olunur — çekin QR kodu oxunmadı.",
                    QrDocId: qrDocId, PhysicalReceiptId: physicalReceiptId,
                    ReceiptDate: receiptDate, ExtractedObjectCode: objectCode,
                    QrVerified: qrVerified,
                    FailReasonCode: "QrMissing",
                    ExtractedTaxNumber: taxNumber);
            }

            // 3c'. Qeyri-fiskal rejimdə filial adı çekdə olmalıdır (konfiqurasiya edilibsə).
            if (nonFiscalMode && !string.IsNullOrWhiteSpace(ctx.ReceiptBranchNames))
            {
                var normText = NormalizeAz(ocrText);
                var nameFound = SplitLabels(ctx.ReceiptBranchNames)
                    .Any(n => normText.Contains(NormalizeAz(n), StringComparison.Ordinal));
                if (!nameFound)
                {
                    return new(false, extracted, carriedReceiptUrl,
                        "Çekdə filial adı tapılmadı. Yenidən aydın şəkil çəkin.",
                        QrDocId: qrDocId, PhysicalReceiptId: physicalReceiptId,
                        ReceiptDate: receiptDate, ExtractedObjectCode: objectCode,
                        QrVerified: qrVerified,
                        FailReasonCode: "BranchNameMismatch",
                        ExtractedTaxNumber: taxNumber,
                        IsNonFiscal: true);
                }
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
                    FailReasonCode: "OcrMismatch",
                    ExtractedTaxNumber: taxNumber,
                    IsNonFiscal: nonFiscalMode);
            }

            if (nonFiscalMode)
                _logger.LogInformation("Non-fiscal receipt accepted. Amount={Amt} ReceiptId={Rid}",
                    extracted, physicalReceiptId);

            return new(true, extracted, carriedReceiptUrl, null,
                QrDocId: qrDocId, PhysicalReceiptId: physicalReceiptId,
                ReceiptDate: receiptDate, ExtractedObjectCode: objectCode,
                QrVerified: qrVerified,
                ExtractedTaxNumber: taxNumber,
                IsNonFiscal: nonFiscalMode);
        }

        private static async Task<byte[]> PreprocessAsync(byte[] input, CancellationToken ct)
        {
            using var image = Image.Load<L8>(input);

            // Ölçünü OCR üçün optimal pəncərəyə gətir: çox böyükdürsə kiçilt (noise/sürət),
            // çox kiçikdirsə böyüt (LSTM ~30px-dən kiçik simvolları tanımır).
            var maxDim = Math.Max(image.Width, image.Height);
            float scale = 1f;
            if (maxDim > MaxOcrDimension) scale = (float)MaxOcrDimension / maxDim;
            else if (maxDim < MinOcrDimension) scale = (float)MinOcrDimension / maxDim;
            if (scale != 1f)
                image.Mutate(x => x.Resize((int)(image.Width * scale), (int)(image.Height * scale)));

            // Termal çek printi solğun/boz + telefon fotosunda kölgə olur. AdaptiveThreshold
            // lokal pəncərəyə görə binarizasiya edir — kölgəli hissələr qaralmır (sabit
            // threshold-dan fərqli). Əvvəl yüngül contrast + sharpen ki, sönük print tündləşsin.
            image.Mutate(x => x
                .Contrast(1.3f)
                .GaussianSharpen(1.2f)
                .AdaptiveThreshold());

            using var outMs = new MemoryStream();
            await image.SaveAsPngAsync(outMs, ct);
            return outMs.ToArray();
        }

        private static string RunOcr(byte[] imageBytes)
        {
            lock (_engineLock)
            {
                var engine = _engine.Value;
                using var pix = Pix.LoadFromMemory(imageBytes);

                // Çek = tək dar sütun → SingleColumn ən düzgün seqmentləşmə.
                // SingleBlock bütün çeki tək blok sayırdı → sətirlər qarışırdı.
                var text = RunOcrPass(engine, pix, PageSegMode.SingleColumn);

                // Nəticə çox qısadırsa (seqmentləşmə uğursuz) köhnə rejimlə təkrar cəhd.
                if (text.Trim().Length < 20)
                {
                    var fallback = RunOcrPass(engine, pix, PageSegMode.SingleBlock);
                    if (fallback.Trim().Length > text.Trim().Length) text = fallback;
                }
                return text;
            }
        }

        private static string RunOcrPass(TesseractEngine engine, Pix pix, PageSegMode psm)
        {
            engine.DefaultPageSegMode = psm;
            using var page = engine.Process(pix);
            return page.GetText() ?? string.Empty;
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
        private const string ObjectCodeValuePattern = @"\s*[:\-]?\s*(?<val>[A-Za-z0-9\-]{3,20})";
        private const string ReceiptIdValuePattern = @"\s*[:\-]?\s*(?<val>[A-Za-z0-9\-]{5,40})";
        private const string TaxNumberValuePattern = @"\s*[:\-]?\s*(?<val>\d{7,15})";

        // Comma-separated label dəstəyi — "Yekun Məbləğ,Cəmi,Total" kimi multi-language list.
        // Hər label-i ardıcıl yoxlayır, birincisi tapan istifadə edir.
        private static IEnumerable<string> SplitLabels(string labelOrList)
        {
            if (string.IsNullOrWhiteSpace(labelOrList)) yield break;
            foreach (var part in labelOrList.Split(','))
            {
                var trimmed = part.Trim();
                if (!string.IsNullOrEmpty(trimmed)) yield return trimmed;
            }
        }

        private static decimal? ExtractByLabel(string ocrText, string labelOrList, string valuePattern)
        {
            if (string.IsNullOrWhiteSpace(ocrText)) return null;
            foreach (var label in SplitLabels(labelOrList))
            {
                var pat = Regex.Escape(label) + valuePattern;
                var m = Regex.Match(ocrText, pat, RegexOptions.IgnoreCase);
                if (!m.Success) continue;
                var raw = m.Groups["val"].Value.Replace(',', '.');
                if (decimal.TryParse(raw, NumberStyles.Number, CultureInfo.InvariantCulture, out var v))
                    return v;
            }
            return null;
        }

        private static string? ExtractByLabelString(string ocrText, string labelOrList, string valuePattern)
        {
            if (string.IsNullOrWhiteSpace(ocrText)) return null;
            foreach (var label in SplitLabels(labelOrList))
            {
                var pat = Regex.Escape(label) + valuePattern;
                var m = Regex.Match(ocrText, pat, RegexOptions.IgnoreCase);
                if (m.Success) return m.Groups["val"].Value;
            }
            return null;
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

        // OCR diakritika xətalarına davamlı müqayisə: upper-invariant + AZ hərfləri ASCII-yə + whitespace collapse.
        private static string NormalizeAz(string s)
        {
            if (string.IsNullOrEmpty(s)) return string.Empty;
            var sb = new System.Text.StringBuilder(s.Length);
            foreach (var ch in s.ToUpperInvariant())
            {
                sb.Append(ch switch
                {
                    'Ə' => 'E', 'Ö' => 'O', 'Ü' => 'U', 'İ' => 'I',
                    'Ş' => 'S', 'Ç' => 'C', 'Ğ' => 'G',
                    _ => ch
                });
            }
            return Regex.Replace(sb.ToString(), @"\s+", " ");
        }

        private static DateTime TodayInTenantZone(string timeZoneId)
        {
            try
            {
                var tz = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
                return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz).Date;
            }
            catch
            {
                return DateTime.UtcNow.Date;
            }
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

using System.Globalization;
using System.Text.RegularExpressions;
using Loyalty.Business.Services.Abstractions.Main;
using Loyalty.Business.Services.Abstractions.Storage.Azure;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using ZXing;
using ZXing.ImageSharp;

namespace Loyalty.Business.Services.Implementations.Main
{
    /// <summary>
    /// Çek yoxlaması: QR (ZXing / scanner) → e-kassa şəkli → Azure OpenAI vision OCR (yeganə mənbə) →
    /// prioritetli müqayisə (tarix pəncərəsi → obyekt kodu → VÖEN/filial adı → QR → məbləğ).
    /// </summary>
    public sealed class ReceiptValidationService : IReceiptValidationService
    {
        private const string ReceiptContainer = "receipts";

        /// <summary>Çek üzərindəki tarix skan günündən ən çox bu qədər gün əvvəl ola bilər (bu gün və ya dünən).</summary>
        internal const int MaxReceiptAgeDays = 1;

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
            string? merchantName = null;
            bool qrVerified = false;
            bool visionFiscal = false;
            string? visionOrderNo = null;

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

            // Step 2: Vision OCR (yeganə mənbə). Tenant label-ləri ipucu kimi gedir; rawText regex fallback üçün.
            string ocrText = string.Empty;
            {
                VisionReceiptFields v;
                try
                {
                    var hints = new VisionReceiptHints(
                        AmountLabels: ctx.OcrLabelAmount,
                        ObjectCodeLabels: ctx.OcrLabelObjectCode,
                        ReceiptIdLabels: ctx.OcrLabelReceiptId,
                        TaxNumberLabels: ctx.OcrLabelTaxNumber);
                    v = await _vision.ReadAsync(ocrBytes, hints, ct);
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Vision OCR threw unexpectedly.");
                    v = new VisionReceiptFields { Ok = false, Transient = true };
                }

                if (!v.Ok)
                {
                    var msg = v.Transient
                        ? "Çek oxuma xidməti müvəqqəti cavab vermədi. Əməliyyat admin yoxlamasına göndərildi."
                        : "Çek oxunmadı. Yenidən aydın şəkil çəkin.";
                    _logger.LogWarning("Vision OCR failed. Transient={Transient}", v.Transient);
                    return new(false, null, carriedReceiptUrl, msg,
                        QrDocId: qrDocId, PhysicalReceiptId: physicalReceiptId,
                        QrVerified: qrVerified,
                        FailReasonCode: "OcrUnreadable");
                }

                ocrText = v.RawText ?? string.Empty;
                merchantName = v.MerchantName;
                visionFiscal = v.IsFiscal;
                visionOrderNo = v.OrderNo;

                // Model sahəni null qoyubsa rawText üzərində köhnə regex-lər fallback.
                extracted = v.Amount
                            ?? ExtractByLabel(ocrText, ctx.OcrLabelAmount, AmountValuePattern)
                            ?? ExtractFallbackAmount(ocrText);
                objectCode = v.ObjectCode ?? ExtractByLabelString(ocrText, ctx.OcrLabelObjectCode, ObjectCodeValuePattern);
                taxNumber = v.TaxNumber ?? ExtractByLabelString(ocrText, ctx.OcrLabelTaxNumber, TaxNumberValuePattern);
                if (string.IsNullOrWhiteSpace(physicalReceiptId))
                    physicalReceiptId = v.ReceiptId ?? ExtractByLabelString(ocrText, ctx.OcrLabelReceiptId, ReceiptIdValuePattern);
                receiptDate = v.Date ?? ExtractDate(ocrText);

                _logger.LogInformation(
                    "Vision OCR ok. Amount={Amount} Date={Date} Tax={Tax} Obj={Obj} Rid={Rid} Fiscal={F} Merchant={M} TextLen={Len}",
                    extracted, receiptDate, taxNumber, objectCode, physicalReceiptId, visionFiscal, merchantName, ocrText.Length);

                if (extracted is null)
                {
                    _logger.LogInformation("OCR found no amount in receipt.");
                    return new(false, null, carriedReceiptUrl,
                        "Çekdə məbləğ tapılmadı. Yenidən aydın şəkil çəkin.",
                        QrDocId: qrDocId, PhysicalReceiptId: physicalReceiptId,
                        ReceiptDate: receiptDate, ExtractedObjectCode: objectCode,
                        QrVerified: qrVerified,
                        FailReasonCode: "OcrUnreadable",
                        ExtractedTaxNumber: taxNumber);
                }
            }

            // Fiskal-lıq detektoru: qeyri-fiskal "Ödəniş çeki"ndə bu əlamətlərin heç biri olmur.
            // AllowNonFiscalReceipts aktivdirsə belə çeklər QR tələbindən keçir (sahib yoxlaması yenə işləyir).
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

            // Step 3: compare (priority — ReceiptOcrJob FailReasonCode-a əsasən IsSuspicious yazır).

            // 3a. Tarix məcburidir və pəncərə daxilində olmalıdır (bu gün və ya dünən, tenant TZ-də).
            if (!receiptDate.HasValue)
            {
                return new(false, extracted, carriedReceiptUrl,
                    "Çekdə tarix oxunmadı. Yenidən aydın şəkil çəkin.",
                    QrDocId: qrDocId, PhysicalReceiptId: physicalReceiptId,
                    ExtractedObjectCode: objectCode,
                    QrVerified: qrVerified,
                    FailReasonCode: "ReceiptDateMissing",
                    ExtractedTaxNumber: taxNumber,
                    IsNonFiscal: nonFiscalMode);
            }
            if (!IsWithinReceiptWindow(receiptDate.Value, ctx.TimeZoneId, MaxReceiptAgeDays))
            {
                return new(false, extracted, carriedReceiptUrl,
                    $"Çekin tarixi ({receiptDate:dd.MM.yyyy}) icazə verilən pəncərədən kənardır (yalnız bu gün və ya dünən).",
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
                    ExtractedTaxNumber: taxNumber,
                    IsNonFiscal: nonFiscalMode);
            }

            // 3c. Sahib yoxlaması: VÖEN VƏ YA filial adı (konfiqurasiya varsa məcburi, rejimdən asılı deyil).
            //     Fərqli VÖEN həmişə rədd (güclü siqnal); heç biri təsdiqlənməyibsə MerchantMismatch.
            var owner = EvaluateOwnership(ctx.ExpectedTaxNumber, ctx.ReceiptBranchNames, taxNumber, merchantName, ocrText);
            if (owner == OwnershipVerdict.TaxMismatch)
            {
                return new(false, extracted, carriedReceiptUrl,
                    $"VÖEN uyğun deyil (gözlənilən {ctx.ExpectedTaxNumber}, tapıldı {taxNumber}).",
                    QrDocId: qrDocId, PhysicalReceiptId: physicalReceiptId,
                    ReceiptDate: receiptDate, ExtractedObjectCode: objectCode,
                    QrVerified: qrVerified,
                    FailReasonCode: "TaxNumberMismatch",
                    ExtractedTaxNumber: taxNumber,
                    IsNonFiscal: nonFiscalMode);
            }
            if (owner == OwnershipVerdict.NotVerified)
            {
                return new(false, extracted, carriedReceiptUrl,
                    "Çekdə gözlənilən VÖEN və ya filial adı tapılmadı.",
                    QrDocId: qrDocId, PhysicalReceiptId: physicalReceiptId,
                    ReceiptDate: receiptDate, ExtractedObjectCode: objectCode,
                    QrVerified: qrVerified,
                    FailReasonCode: "MerchantMismatch",
                    ExtractedTaxNumber: taxNumber,
                    IsNonFiscal: nonFiscalMode);
            }

            // 3d. QR missing strict mode (qeyri-fiskal rejim QR tələbindən azaddır)
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

            // 3e. Amount tolerance — çek yekunu konfiqurasiyalı faiz qədər azaldılıb (xidmət haqqı) müqayisə olunur.
            var entered = Math.Abs(ctx.EnteredAmount);
            var tol = ctx.ToleranceAzn <= 0 ? 1m : ctx.ToleranceAzn;
            var pct = ctx.AmountDeductionPercent;
            var net = NetAmount(extracted.Value, pct);
            var diff = Math.Abs(net - entered);
            if (diff > tol)
            {
                _logger.LogWarning("OCR amount mismatch. Entered={Entered}, Ocr={Ocr}, Pct={Pct}, Net={Net}, Tolerance={Tol}, Diff={Diff}",
                    entered, extracted, pct, net, tol, diff);
                var msg = pct > 0
                    ? $"Çek yekunu {extracted:0.##} (−{pct:0.##}% = {net:0.##}) daxil edilən məbləğlə ({entered:0.##}) uyğun deyil."
                    : $"Çek məbləği ({extracted:0.##}) daxil edilən məbləğlə ({entered:0.##}) uyğun deyil.";
                return new(false, extracted, carriedReceiptUrl, msg,
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

        // ---------- Qaydalar (test edilə bilən, pure) ----------

        internal enum OwnershipVerdict { NotConfigured, Verified, TaxMismatch, NotVerified }

        /// <summary>
        /// VÖEN və ya filial adı: konfiqurasiya yoxdursa NotConfigured; çekdəki VÖEN gözləniləndən fərqlidirsə TaxMismatch;
        /// VÖEN uyğundursa və ya ad tapılıbsa Verified; əks halda NotVerified.
        /// </summary>
        internal static OwnershipVerdict EvaluateOwnership(
            string? expectedTaxNumber, string? branchNamesCsv, string? foundTaxNumber, string? merchantName, string rawText)
        {
            var expectedTax = NormalizeTax(expectedTaxNumber);
            var names = SplitLabels(branchNamesCsv ?? string.Empty).ToList();
            if (expectedTax.Length == 0 && names.Count == 0) return OwnershipVerdict.NotConfigured;

            var foundTax = NormalizeTax(foundTaxNumber);
            bool taxOk = expectedTax.Length > 0 && foundTax.Length > 0 && expectedTax == foundTax;
            bool taxMismatch = expectedTax.Length > 0 && foundTax.Length > 0 && !taxOk;
            if (taxMismatch) return OwnershipVerdict.TaxMismatch;

            bool nameOk = names.Count > 0 && ContainsAnyName(merchantName, rawText, names);
            return (taxOk || nameOk) ? OwnershipVerdict.Verified : OwnershipVerdict.NotVerified;
        }

        internal static bool ContainsAnyName(string? merchantName, string rawText, IEnumerable<string> names)
        {
            var hay = NormalizeAz((merchantName ?? string.Empty) + "\n" + (rawText ?? string.Empty));
            if (hay.Trim().Length == 0) return false;
            foreach (var n in names)
            {
                var needle = NormalizeAz(n).Trim();
                if (needle.Length > 0 && hay.Contains(needle, StringComparison.Ordinal)) return true;
            }
            return false;
        }

        /// <summary>
        /// VÖEN müqayisəsi üçün: rəqəm varsa yalnız rəqəmlər ("VÖEN: 1701 332 401" → "1701332401");
        /// rəqəmsiz dəyərdə hərf-rəqəm upper.
        /// </summary>
        internal static string NormalizeTax(string? s)
        {
            if (string.IsNullOrWhiteSpace(s)) return string.Empty;
            var digits = new string(s.Where(char.IsDigit).ToArray());
            if (digits.Length > 0) return digits;
            return new string(s.Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();
        }

        /// <summary>Çek yekununu faiz qədər azaldır (xidmət haqqı), 2 onluq.</summary>
        internal static decimal NetAmount(decimal gross, decimal deductionPercent)
        {
            if (deductionPercent <= 0) return gross;
            if (deductionPercent >= 100) return 0m;
            return Math.Round(gross * (1m - deductionPercent / 100m), 2, MidpointRounding.AwayFromZero);
        }

        /// <summary>Çek tarixi tenant TZ-də bu gün və ya ən çox maxAgeDays gün əvvəl olmalıdır; gələcək tarix rədd.</summary>
        internal static bool IsWithinReceiptWindow(DateTime receiptDate, string timeZoneId, int maxAgeDays, DateTime? utcNow = null)
        {
            var now = utcNow ?? DateTime.UtcNow;
            DateTime todayLocal, receiptLocal;
            try
            {
                var tz = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
                todayLocal = TimeZoneInfo.ConvertTimeFromUtc(now, tz).Date;
                var receiptUtc = receiptDate.Kind == DateTimeKind.Utc
                    ? receiptDate
                    : DateTime.SpecifyKind(receiptDate, DateTimeKind.Utc);
                receiptLocal = TimeZoneInfo.ConvertTimeFromUtc(receiptUtc, tz).Date;
            }
            catch
            {
                // TZ tapılmadısa UTC fallback
                todayLocal = now.Date;
                receiptLocal = receiptDate.Date;
            }
            var age = (todayLocal - receiptLocal).Days;
            return age >= 0 && age <= maxAgeDays;
        }

        // ---------- rawText regex fallback (model sahəni null qoyanda) ----------

        private const string AmountValuePattern = @"\s*[:\-]?\s*(?<val>\d{1,5}[.,]\d{2})";
        private const string ObjectCodeValuePattern = @"\s*[:\-]?\s*(?<val>[A-Za-z0-9\-]{3,20})";
        private const string ReceiptIdValuePattern = @"\s*[:\-]?\s*(?<val>[A-Za-z0-9\-]{5,40})";
        private const string TaxNumberValuePattern = @"\s*[:\-]?\s*(?<val>\d{7,15})";

        // Comma-separated label dəstəyi — "Yekun Məbləğ,Cəmi,Total" kimi multi-language list.
        internal static IEnumerable<string> SplitLabels(string labelOrList)
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

        // Label tapılmadıqda mətndə ən böyük decimal-ı götür.
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

        // Diakritika xətalarına davamlı müqayisə: upper-invariant + AZ hərfləri ASCII-yə + whitespace collapse.
        internal static string NormalizeAz(string s)
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
    }
}

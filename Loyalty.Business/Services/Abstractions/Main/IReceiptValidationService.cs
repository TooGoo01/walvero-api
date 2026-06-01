using Microsoft.AspNetCore.Http;

namespace Loyalty.Business.Services.Abstractions.Main
{
    /// <summary>
    /// Scanner-dən gələn e-kassa SPA JSON capture nəticəsi. WebView reCAPTCHA təbii icra edib
    /// `getDocumentPict` cavabını intercept edir, sonra Flutter bu struct-u multipart payload-a
    /// `qrVerifiedJson` field-i kimi əlavə edir. Bütün dəyərlər opsionaldır — JSON shape dəyişə bilər.
    /// </summary>
    public sealed record QrVerifiedPayload(
        string? DocId,
        decimal? Amount,
        DateTime? CreatedAtUtc,
        string? CashBoxObjectCode,
        string? DocumentNumber,
        string? TaxNumber);

    /// <summary>
    /// Validation üçün context: cfg-dən gələn dynamic config + scanner-side optional inputs.
    /// </summary>
    public sealed class ReceiptValidationContext
    {
        public decimal EnteredAmount { get; init; }
        public decimal ToleranceAzn { get; init; }
        public int TenantId { get; init; }
        public int? BranchId { get; init; }
        public string OcrLabelAmount { get; init; } = "Yekun Məbləğ";
        public string OcrLabelObjectCode { get; init; } = "Obyekt Kodu";
        public string OcrLabelReceiptId { get; init; } = "Çek nömrəsi";
        public string TimeZoneId { get; init; } = "Azerbaijan Standard Time";
        public string? BranchObjectCode { get; init; }
        public bool RequireQrVerification { get; init; }
        public string? QrDocIdFromScanner { get; init; }
        public QrVerifiedPayload? QrVerifiedPayload { get; init; }
    }

    public sealed record ReceiptValidationResult(
        bool IsValid,
        decimal? ExtractedAmount,
        string? ReceiptUrl,
        string? Error,
        string? QrDocId = null,
        string? PhysicalReceiptId = null,
        DateTime? ReceiptDate = null,
        string? ExtractedObjectCode = null,
        bool QrVerified = false,
        string? FailReasonCode = null);

    public interface IReceiptValidationService
    {
        Task<ReceiptValidationResult> ValidateAsync(
            IFormFile image,
            ReceiptValidationContext ctx,
            CancellationToken ct = default);

        /// <summary>
        /// Blob upload yox — yalnız bytes üzərində OCR + QR yoxlaması. Hangfire job-da istifadə olunur.
        /// </summary>
        Task<ReceiptValidationResult> ValidateBytesAsync(
            byte[] imageBytes,
            ReceiptValidationContext ctx,
            string? existingReceiptUrl,
            CancellationToken ct = default);

        /// <summary>
        /// Yalnız Azure blob upload edir, OCR yox. PendingOcr axınında ilk request-də istifadə olunur.
        /// Qaytarır: (BlobFileName, FullUrl).
        /// </summary>
        Task<(string fileName, string url)> UploadReceiptOnlyAsync(
            IFormFile image,
            CancellationToken ct = default);
    }
}

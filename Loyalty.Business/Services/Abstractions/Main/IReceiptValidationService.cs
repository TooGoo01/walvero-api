using Microsoft.AspNetCore.Http;

namespace Loyalty.Business.Services.Abstractions.Main
{
    /// <summary>
    /// Validation üçün context: cfg-dən gələn dynamic config + scanner-side optional inputs.
    /// </summary>
    public sealed class ReceiptValidationContext
    {
        public decimal EnteredAmount { get; init; }
        public decimal ToleranceAzn { get; init; }
        public int TenantId { get; init; }
        public int? BranchId { get; init; }
        public string OcrLabelAmount { get; init; } = "Yekun Məbləğ,Cəmi,Total,Sum";
        public string OcrLabelObjectCode { get; init; } = "Obyektin kodu,Obyekt Kodu,Object Code";
        public string OcrLabelReceiptId { get; init; } = "Fiskal İD,Fiskal ID,Fiscal ID,Çek nömrəsi";
        public string OcrLabelTaxNumber { get; init; } = "VÖEN,VOEN,Vergi nömrəsi,Tax number,TIN";
        public string TimeZoneId { get; init; } = "Azerbaijan Standard Time";
        public string? BranchObjectCode { get; init; }
        public string? ExpectedTaxNumber { get; init; }
        public bool RequireQrVerification { get; init; }
        public string? QrDocIdFromScanner { get; init; }
        // Qeyri-fiskal slip qəbulu: fiskal əlaməti olmayan çek QR tələbindən keçir,
        // əvəzində ReceiptBranchNames OCR mətnində axtarılır (BranchNameMismatch reject).
        public bool AllowNonFiscalReceipts { get; init; }
        public string? ReceiptBranchNames { get; init; }
        // E-kassa monitoring endpoint URL template (Tenant-dən gəlir). `{docId}` placeholder.
        public string EkassaUrlTemplate { get; init; }
            = "https://monitoring.e-kassa.gov.az/pks-monitoring/2.0.0/documents/{docId}";
        // Flutter scanner-in AZ-network-dən fetch etdiyi e-kassa JPEG bytes — priority source.
        // Set olsa OCR bu image-də işləyir, qrVerified=true. Olmasa backend EkassaImageFetcher
        // cəhd edir (firewall block ehtimalı yüksək), uğursuz olarsa user photo-ya fallback.
        public byte[]? EkassaImageBytes { get; init; }
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
        string? FailReasonCode = null,
        string? ExtractedTaxNumber = null,
        bool IsNonFiscal = false);

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

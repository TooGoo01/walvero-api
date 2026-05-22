using Microsoft.AspNetCore.Http;

namespace Loyalty.Business.Services.Abstractions.Main
{
    public sealed record ReceiptValidationResult(
        bool IsValid,
        decimal? ExtractedAmount,
        string? ReceiptUrl,
        string? Error);

    public interface IReceiptValidationService
    {
        Task<ReceiptValidationResult> ValidateAsync(
            IFormFile image,
            decimal enteredAmount,
            decimal toleranceAzn,
            CancellationToken ct = default);

        /// <summary>
        /// Blob upload yox — yalnız bytes üzərində OCR yoxlaması. Hangfire job-da istifadə olunur.
        /// </summary>
        Task<ReceiptValidationResult> ValidateBytesAsync(
            byte[] imageBytes,
            decimal enteredAmount,
            decimal toleranceAzn,
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

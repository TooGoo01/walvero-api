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
    }
}

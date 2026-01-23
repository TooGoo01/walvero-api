// Business/Services/Abstractions/Main/IGoogleWalletService.cs
using Loyalty.Business.Dtos.Post.Main;
using Loyalty.Business.Results;

namespace Loyalty.Business.Services.Abstractions.Main
{
    public interface IGoogleWalletService
    {
        /// Class-ı (LoyaltyClass) mövcud deyilsə yaradır; rəng, logo, hero və s. tpl-dən götürülür
        Task EnsureClassAsync(
            CardTemplateSnapshot tpl,
            string programName,
            CancellationToken ct = default);

        /// Kart üçün LoyaltyObject yaradır və ya mövcudu qaytarır (ikon/başlıq/metn – dinamik)
        Task<ServiceResult> CreateOrGetObjectByCardIdAsync(
            int loyaltyCardId,
            CardTemplateSnapshot tpl,
            string? accountName = null,
            string? barcodeValue = null,
            int currentPoints = 0,
            int currentSteps = 0,
            CancellationToken ct = default);

        /// “Save to Google Wallet” linki (JWT) yaradır
        Task<ServiceResult> GenerateSaveLinkByCardIdAsync(
            int loyaltyCardId,
            CancellationToken ct = default);

        /// Walvero pass üçün object yaradır (xüsusi object ID formatı ilə)
        Task<ServiceResult> CreateOrGetWalveroObjectAsync(
            string objectId,
            CardTemplateSnapshot tpl,
            string accountName,
            string barcodeValue,
            CancellationToken ct = default);

        /// Walvero pass üçün "Save to Google Wallet" linki yaradır
        Task<ServiceResult> GenerateWalveroSaveLinkAsync(
            string objectId,
            CancellationToken ct = default);

        /// Get IssuerId for constructing object IDs
        string GetIssuerId();

      
        Task<ServiceResult> RedeemByCardIdAsync(
     int loyaltyCardId,
     CardTemplateSnapshot tpl,
     int currentPoints,
     int currentSteps,
     int pointsDelta, string? accountName = null,
     CancellationToken ct = default);
        // (Legacy) skan edilmiş koddan id çıxarıb redeem edən versiya
        Task<ServiceResult> RedeemAsync(
            string scannedCode,
            int pointsDelta = 1,
            CancellationToken ct = default);
    }
}

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
            decimal currentPoints = 0,
            int currentSteps = 0,
            CancellationToken ct = default,
            int lotteryTickets = 0,
            string? lotteryWinPrize = null,
            DateTime? lotteryDrawDate = null);

        /// “Save to Google Wallet” linki (JWT) yaradır
        Task<ServiceResult> GenerateSaveLinkByCardIdAsync(
            int loyaltyCardId,
            int tenantId,
            int programId,
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

        /// Build deterministic Google Wallet object ID for a loyalty card.
        string BuildObjectIdForCard(int tenantId, int programId, int loyaltyCardId);

        /// Push a message ("notification") onto the LoyaltyObject — wallet will surface it to the user.
        Task<ServiceResult> AddMessageToObjectAsync(
            string objectId,
            string title,
            string body,
            CancellationToken ct = default);

      
        Task<ServiceResult> RedeemByCardIdAsync(
     int loyaltyCardId,
     CardTemplateSnapshot tpl,
     decimal currentPoints,
     int currentSteps,
     int pointsDelta, string? accountName = null,
     CancellationToken ct = default,
     int lotteryTickets = 0,
     string? lotteryWinPrize = null,
     DateTime? lotteryDrawDate = null);
        // (Legacy) skan edilmiş koddan id çıxarıb redeem edən versiya
        Task<ServiceResult> RedeemAsync(
            string scannedCode,
            int pointsDelta = 1,
            CancellationToken ct = default);
    }
}

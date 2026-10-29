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
            DateTime? lotteryDrawDate = null,
            int? completedCyclesOverride = null,
            string? objectIdOverride = null,
            string? classIdOverride = null,
            bool updateIfExists = false,
            decimal? lifetimePointsOverride = null,
            string? pointsExpiryOverride = null,
            decimal? discountOverride = null);

        /// Publish-siz test: {ClassId}-preview class + preview obyekt → Save-to-Wallet linki (Data: {url, classId, objectId, classStatus})
        Task<ServiceResult> PreviewAsync(
            CardTemplateSnapshot tpl, string previewKey, string accountName, string barcodeValue,
            decimal points, int steps, int cycles, CancellationToken ct = default,
            decimal lifetimePoints = 0, string? pointsExpiryText = null, decimal? discountPercent = null);

        /// Real class vəziyyəti (read-only)
        Task<ServiceResult> GetClassStatusAsync(CancellationToken ct = default);

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

        /// Kart dizayneri: mövcud class-ın programName/issuerName/rəng/logo-sunu tpl-ə uyğunlaşdır (reviewStatus toxunulmur).
        /// Class yoxdursa yaradır. Runtime (redeem) yolunda ÇAĞIRILMIR — yalnız admin saxlama / refresh.
        Task<ServiceResult> UpdateClassDesignAsync(CardTemplateSnapshot tpl, CancellationToken ct = default);

        /// Kart obyektinin vəziyyəti (bağlanmış kart → "INACTIVE", geri açılan → "ACTIVE"). Obyekt yoxdursa no-op (uğurlu).
        Task<ServiceResult> SetObjectStateAsync(int tenantId, int programId, int loyaltyCardId, string state, CancellationToken ct = default);

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
     DateTime? lotteryDrawDate = null,
     int? completedCyclesOverride = null);
        // (Legacy) skan edilmiş koddan id çıxarıb redeem edən versiya
        Task<ServiceResult> RedeemAsync(
            string scannedCode,
            int pointsDelta = 1,
            CancellationToken ct = default);
    }
}

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

        /// Kassir əməliyyatı (redeem/add). DB-də yenilənmiş p/u dəyərləri göndərilir,
        /// Wallet tərəfdə text/ikon/points dinamik patch olunur.
        Task<ServiceResult> RedeemByCardIdAsync(
     int loyaltyCardId,
     CardTemplateSnapshot tpl,
     int currentPoints,
     int currentSteps,
     int pointsDelta,
     CancellationToken ct = default);
        // (Legacy) skan edilmiş koddan id çıxarıb redeem edən versiya
        Task<ServiceResult> RedeemAsync(
            string scannedCode,
            int pointsDelta = 1,
            CancellationToken ct = default);
    }
}

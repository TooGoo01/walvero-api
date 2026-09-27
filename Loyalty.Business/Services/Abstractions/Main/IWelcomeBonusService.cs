using Loyalty.DataAccess.Entities.Main;
using Loyalty.DataAccess.Enums;

namespace Loyalty.Business.Services.Abstractions.Main
{
    /// <summary>
    /// Xoş gəldin bonusu: ilk sifariş bonusu + kart yaradılanda hədiyyə.
    /// Metodlar tracked kartı dəyişir və Bonus Transaction əlavə edir, amma SaveChanges ÇAĞIRMIR —
    /// commit çağıranındır ki, earn ilə atomik olsun. Qaytarılan dəyər verilən bonus (0 = verilmədi).
    /// </summary>
    public interface IWelcomeBonusService
    {
        Task<decimal> TryGrantFirstPurchaseAsync(
            LoyaltyCard card, LoyaltyProgram program, CardTemplateType templateType, int totalSteps,
            decimal? orderAmount, int? branchId, string? orderRef, CancellationToken ct = default);

        Task<decimal> TryGrantSignupGiftAsync(
            LoyaltyCard card, LoyaltyProgram program, CardTemplateType templateType, int totalSteps,
            CancellationToken ct = default);

        /// <summary>iiko wallet balansını yeni CurrentPoints-ə uyğunlaşdır (ayrı scope, best-effort).</summary>
        Task SyncPosBalanceSafeAsync(int tenantId, int customerId, decimal currentPoints, CancellationToken ct = default);
    }
}

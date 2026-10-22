using Loyalty.DataAccess.Entities.Main;

namespace Loyalty.Business.Services.Abstractions.Main
{
    /// <summary>
    /// iiko-da telefonla tapılan mövcud qonaq. WalletId/Balance — proqramın cüzdanı (tapılma anında);
    /// ConflictCustomerId doludursa həmin iiko qonağı bu inteqrasiyada başqa Walvero müştərisinə bağlıdır (bağlanmır).
    /// </summary>
    public sealed record IikoPhoneMatch(Guid IikoCustomerId, Guid? WalletId, decimal Balance, int? ConflictCustomerId)
    {
        public bool IsConflict => ConflictCustomerId.HasValue;
    }

    /// <summary>
    /// Telefonla mövcud iiko qonağına bağlanma + onun köhnə cüzdan balansını Walvero kartına ƏLAVƏ etmə
    /// (PosSyncService, CustomerSyncService, admin dialoq və toplu job ortaq istifadə edir).
    /// </summary>
    public interface IIikoLegacyBalanceService
    {
        /// <summary>Telefonla iiko qonağını axtar. Telefon yoxdur / tapılmadı / iiko xətası → null.</summary>
        Task<IikoPhoneMatch?> FindByPhoneAsync(TenantIntegration integration, string accessToken, Guid orgId, Customer customer, CancellationToken ct = default);

        /// <summary>
        /// Müştərinin mapping-lərində gözləyən köhnə balansları (LegacyBalance&gt;0, LegacyImportedAt null) aktiv karta əlavə et.
        /// Idempotent (Transaction.OrderId "iiko-legacy:{iikoId}:{walletId}"). Aktiv kart yoxdursa gözləyir. Qaytarır: əlavə olunan məbləğ.
        /// </summary>
        Task<decimal> ImportPendingAsync(int tenantId, int customerId, CancellationToken ct = default);
    }
}

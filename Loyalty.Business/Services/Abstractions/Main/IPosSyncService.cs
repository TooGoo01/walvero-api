using Loyalty.DataAccess.Entities.Main;

namespace Loyalty.Business.Services.Abstractions.Main
{
    public interface IPosSyncService
    {
        Task SyncCustomerToPosAsync(int tenantId, Customer customer, CancellationToken ct = default);
        /// <summary>
        /// iiko wallet-i currentPoints-ə bərabərləşdir (topup/chargeoff). Qaytarır: true = iiko inteqrasiyası yoxdur
        /// və ya bütün mapping-li org-larda uğurlu; false = ən azı bir topup/chargeoff alınmadı (və ya xəta).
        /// </summary>
        Task<bool> SyncBalanceToPosAsync(int tenantId, int customerId, decimal currentPoints, CancellationToken ct = default);

        /// <summary>
        /// Telefonla mövcud iiko qonağına bağlanmadan sonra: gözləyən köhnə balansı Walvero kartına əlavə et,
        /// sonra iiko cüzdanını Walvero balansına bərabərləşdir. Best-effort (xəta log-lanır).
        /// </summary>
        Task FinalizePhoneMatchAsync(int tenantId, int customerId, CancellationToken ct = default);

        /// <summary>
        /// Ambassador qonaq kateqoriyası: inteqrasiyada AmbassadorIikoCategoryId varsa müştərini hər iiko org-da
        /// kateqoriyaya əlavə et (isAmbassador) və ya çıxar. Təyin edilməyibsə no-op (true).
        /// </summary>
        Task<bool> SyncAmbassadorCategoryAsync(int tenantId, int customerId, bool isAmbassador, CancellationToken ct = default);

        /// <summary>
        /// Kassa endirim səviyyəsi: müştərini hər iiko org-da köhnə qonaq kateqoriyasından çıxar (old≠new), yenisinə yaz.
        /// Mapping olmayan org atlanır — heç birində yoxdursa NoMapping (müştəri kassada hələ yaradılmayıb).
        /// </summary>
        Task<PosCategorySyncResult> SetCustomerGuestCategoryAsync(int tenantId, int customerId, Guid? oldCategoryId, Guid? newCategoryId, CancellationToken ct = default);
    }

    public class PosCategorySyncResult
    {
        public bool AllOk { get; set; } = true;
        /// <summary>Tenant-da aktiv iiko inteqrasiyası yoxdur — yalnız Walvero-da saxlanıldı.</summary>
        public bool NoIntegration { get; set; }
        /// <summary>Heç bir org-da iiko müştəri mapping-i yoxdur — kassaya yazılmadı.</summary>
        public bool NoMapping { get; set; }
        public List<string> Notes { get; set; } = new();
    }
}

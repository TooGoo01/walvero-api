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
    }
}

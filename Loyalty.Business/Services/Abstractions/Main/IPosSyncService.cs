using Loyalty.DataAccess.Entities.Main;

namespace Loyalty.Business.Services.Abstractions.Main
{
    public interface IPosSyncService
    {
        Task SyncCustomerToPosAsync(int tenantId, Customer customer, CancellationToken ct = default);
    }
}

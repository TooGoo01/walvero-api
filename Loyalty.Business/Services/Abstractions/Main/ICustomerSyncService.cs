namespace Loyalty.Business.Services.Abstractions.Main
{
    public interface ICustomerSyncService
    {
        Task<bool> SyncCustomerToPosAsync(int tenantId, int customerId, CancellationToken ct = default);
        Task<int> SyncAllCustomersToPosAsync(int tenantId, CancellationToken ct = default);
    }
}

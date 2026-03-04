namespace Loyalty.Business.Services.Abstractions.Main
{
    public interface ICustomerSyncService
    {
        Task<CustomerSyncResult> SyncCustomerToPosAsync(int tenantId, int customerId, CancellationToken ct = default);
        Task<int> SyncAllCustomersToPosAsync(int tenantId, CancellationToken ct = default);
    }

    public class CustomerSyncResult
    {
        public bool Success { get; set; }
        public long? WalveroId { get; set; }
        public string? Barcode { get; set; }

        public static CustomerSyncResult Fail() => new() { Success = false };
        public static CustomerSyncResult Ok(long? walveroId, string? barcode) => new() { Success = true, WalveroId = walveroId, Barcode = barcode };
    }
}

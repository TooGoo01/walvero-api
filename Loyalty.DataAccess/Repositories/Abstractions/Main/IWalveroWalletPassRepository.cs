using Loyalty.DataAccess.Entities.Main;

namespace Loyalty.DataAccess.Repositories.Abstractions.Main
{
    /// <summary>
    /// Repository for WalveroWalletPass entities.
    /// </summary>
    public interface IWalveroWalletPassRepository : IGenericRepository<WalveroWalletPass>
    {
        /// <summary>
        /// Get or create a WalveroWalletPass record for a user and tenant.
        /// Idempotent operation - returns existing record if found.
        /// </summary>
        Task<WalveroWalletPass> GetOrCreateAsync(
            int applicationUserId,
            int tenantId,
            string walveroIdMasked,
            CancellationToken ct = default);

        /// <summary>
        /// Get existing pass record for user and tenant.
        /// </summary>
        Task<WalveroWalletPass?> GetByUserAndTenantAsync(
            int applicationUserId,
            int tenantId,
            CancellationToken ct = default);

        /// <summary>
        /// Update pass URLs and platform flags.
        /// </summary>
        Task UpdatePassUrlsAsync(
            int passId,
            string? applePassUrl,
            string? googleSaveUrl,
            string? appleSerialNumber,
            string? googleObjectId,
            bool hasIosPass,
            bool hasAndroidPass,
            CancellationToken ct = default);
    }
}


using Loyalty.DataAccess.Entities.Main;

namespace Loyalty.DataAccess.Repositories.Abstractions.Main
{
    public interface ITenantIntegrationRepository : IGenericRepository<TenantIntegration>
    {
        Task<TenantIntegration?> GetByWalletTokenHashAsync(string walletTokenHash, CancellationToken ct = default);
        Task<TenantIntegration?> GetByVenueAndSourceAsync(string venueId, string source, CancellationToken ct = default);
        Task<TenantIntegration?> GetActiveByTenantAndTypeAsync(int tenantId, int integrationTypeId, CancellationToken ct = default);
        Task<TenantIntegration?> GetActiveByTenantAndSourceAsync(int tenantId, string source, CancellationToken ct = default);
        Task<TenantIntegration?> GetActiveByTenantAsync(int tenantId, CancellationToken ct = default);
        Task<List<TenantIntegration>> GetAllActiveByTenantAsync(int tenantId, CancellationToken ct = default);
    }
}

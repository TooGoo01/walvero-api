using Loyalty.DataAccess.Entities.Main;

namespace Loyalty.DataAccess.Repositories.Abstractions.Main
{
    public interface IBranchRepository : IGenericRepository<Branch>
    {
        Task<IEnumerable<Branch>> GetByTenantAsync(int tenantId, CancellationToken ct = default);
        Task<Branch?> GetByIdAsync(int id, CancellationToken ct = default);
        Task<IEnumerable<Branch>> GetNearbyAsync(int tenantId, double lat, double lng, double radiusKm, CancellationToken ct = default);
    }
}

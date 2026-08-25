using Loyalty.DataAccess.Entities.Main;

namespace Loyalty.DataAccess.Repositories.Abstractions.Main
{
    public interface IBranchRepository : IGenericRepository<Branch>
    {
        Task<IEnumerable<Branch>> GetByTenantAsync(int tenantId, CancellationToken ct = default);
        Task<Branch?> GetByIdAsync(int id, CancellationToken ct = default);
        Task<IEnumerable<Branch>> GetNearbyAsync(int tenantId, double lat, double lng, double radiusKm, CancellationToken ct = default);

        /// <summary>
        /// AI place-discovery köməkçisi üçün: bütün görünən (aktiv, gizlədilməmiş) filiallar,
        /// Tenant + Category + TenantLocation daxil. Keyword/geo filtri servis qatında (in-memory) tətbiq olunur.
        /// </summary>
        Task<List<Branch>> GetDiscoverableAsync(CancellationToken ct = default);
    }
}

using Loyalty.DataAccess.Context;
using Loyalty.DataAccess.Entities.Main;
using Loyalty.DataAccess.Repositories.Abstractions.Main;
using Microsoft.EntityFrameworkCore;

namespace Loyalty.DataAccess.Repositories.Implementations.Main
{
    public class BranchRepository : GenericRepository<Branch>, IBranchRepository
    {
        private readonly LoyaltyWalletDbContext _context;

        public BranchRepository(LoyaltyWalletDbContext context) : base(context)
        {
            _context = context;
        }

        public async Task<IEnumerable<Branch>> GetByTenantAsync(int tenantId, CancellationToken ct = default)
        {
            return await GetAsQueryable()
                .Include(b => b.Staff)
                .Where(b => b.TenantId == tenantId && b.IsActive && !b.HideInApp)
                .OrderByDescending(b => b.IsMainBranch)
                .ThenBy(b => b.Name)
                .ToListAsync(ct);
        }

        public new async Task<Branch?> GetByIdAsync(int id, CancellationToken ct = default)
        {
            return await GetAsQueryable()
                .Include(b => b.Staff)
                .Include(b => b.Transactions)
                .FirstOrDefaultAsync(b => b.Id == id, ct);
        }

        public async Task<List<Branch>> GetDiscoverableAsync(CancellationToken ct = default)
        {
            // Görünən filiallar: filial aktiv & app-da gizli deyil, tenant aktiv & gizli/silinmiş/app-da gizli deyil.
            // Tenant qrafiki (Category, Branding, TenantLocation) keyword/menu üçün daxil edilir.
            return await GetAsQueryable()
                .AsNoTracking()
                .Include(b => b.Tenant!).ThenInclude(t => t!.Category)
                .Include(b => b.Tenant!).ThenInclude(t => t!.Branding)
                .Include(b => b.Tenant!).ThenInclude(t => t!.TenantLocation)
                .Where(b => b.IsActive
                    && !b.HideInApp
                    && b.Tenant != null
                    && b.Tenant.IsActive
                    && !b.Tenant.IsHidden
                    && !b.Tenant.IsDeleted
                    && !b.Tenant.HideInApp)
                .ToListAsync(ct);
        }

        public async Task<IEnumerable<Branch>> GetNearbyAsync(int tenantId, double lat, double lng, double radiusKm, CancellationToken ct = default)
        {
            var latRad = lat * Math.PI / 180.0;
            var lngRad = lng * Math.PI / 180.0;

            // Use Haversine formula: 6371 * acos(cos(lat1)*cos(lat2)*cos(lng2-lng1) + sin(lat1)*sin(lat2))
            var branches = await GetAsQueryable()
                .Include(b => b.Staff)
                .Where(b => b.TenantId == tenantId
                    && b.IsActive
                    && !b.HideInApp
                    && b.Latitude.HasValue
                    && b.Longitude.HasValue)
                .ToListAsync(ct);

            // Calculate distances in-memory (Haversine is not translatable to SQL by EF Core)
            return branches
                .Select(b =>
                {
                    var bLatRad = (double)b.Latitude!.Value * Math.PI / 180.0;
                    var bLngRad = (double)b.Longitude!.Value * Math.PI / 180.0;

                    var cosDistance = Math.Cos(latRad) * Math.Cos(bLatRad) * Math.Cos(bLngRad - lngRad)
                                   + Math.Sin(latRad) * Math.Sin(bLatRad);

                    // Clamp to [-1, 1] to avoid NaN from floating-point errors
                    cosDistance = Math.Max(-1.0, Math.Min(1.0, cosDistance));

                    var distanceKm = 6371.0 * Math.Acos(cosDistance);
                    return new { Branch = b, DistanceKm = distanceKm };
                })
                .Where(x => x.DistanceKm <= radiusKm)
                .OrderBy(x => x.DistanceKm)
                .Select(x =>
                {
                    // Store distance in a transient way — service layer will read coordinates to recalculate
                    return x.Branch;
                })
                .ToList();
        }
    }
}

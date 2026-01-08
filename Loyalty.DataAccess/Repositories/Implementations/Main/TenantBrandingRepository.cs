using Loyalty.DataAccess.Context;
using Loyalty.DataAccess.Entities.Main;
using Loyalty.DataAccess.Repositories.Abstractions.Main;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Loyalty.DataAccess.Repositories.Implementations.Main
{
    public class TenantBrandingRepository : GenericRepository<TenantBranding>, ITenantBrandingRepository
    {
        public TenantBrandingRepository(LoyaltyWalletDbContext LoyaltyDbContext) : base(LoyaltyDbContext)
        {
        }

        public Task<TenantBranding?> GetById(int id, CancellationToken ct = default)
        => GetAsQueryable().AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);

        public Task<TenantBranding?> GetByTenantIdAsync(int tenantId, CancellationToken ct = default)
        => GetAsQueryable().AsNoTracking().FirstOrDefaultAsync(x => x.TenantId == tenantId, ct);

        // OPTIMIZATION: Batch method to fetch brandings for multiple tenants in single query
        public async Task<Dictionary<int, TenantBranding>> GetByTenantIdsAsync(IEnumerable<int> tenantIds, CancellationToken ct = default)
        {
            var ids = tenantIds?.Distinct().ToList() ?? new List<int>();
            if (!ids.Any())
                return new Dictionary<int, TenantBranding>();

            var brandings = await GetAsQueryable()
                .AsNoTracking()
                .Where(b => ids.Contains(b.TenantId))
                .ToListAsync(ct);

            return brandings.ToDictionary(b => b.TenantId);
        }

        public async Task UpsertAsync(int tenantId, TenantBranding branding, CancellationToken ct = default)
        {
           throw new NotImplementedException();
        }
    }
}

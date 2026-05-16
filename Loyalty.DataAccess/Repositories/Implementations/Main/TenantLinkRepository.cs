using Loyalty.DataAccess.Context;
using Loyalty.DataAccess.Entities.Main;
using Loyalty.DataAccess.Repositories.Abstractions.Main;
using Microsoft.EntityFrameworkCore;

namespace Loyalty.DataAccess.Repositories.Implementations.Main
{
    public class TenantLinkRepository : GenericRepository<TenantLink>, ITenantLinkRepository
    {
        public TenantLinkRepository(LoyaltyWalletDbContext loyaltyDbContext) : base(loyaltyDbContext)
        {
        }

        public async Task<IEnumerable<TenantLink>> GetByTenantIdOrderedAsync(int tenantId)
        {
            return await GetAsQueryable()
                .Where(tl => tl.TenantId == tenantId && tl.IsActive)
                .OrderBy(tl => tl.SortOrder)
                .ThenBy(tl => tl.Id)
                .ToListAsync();
        }
    }
}

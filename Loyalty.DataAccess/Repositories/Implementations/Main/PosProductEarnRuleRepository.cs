using Loyalty.DataAccess.Context;
using Loyalty.DataAccess.Entities.Main;
using Loyalty.DataAccess.Repositories.Abstractions.Main;
using Microsoft.EntityFrameworkCore;

namespace Loyalty.DataAccess.Repositories.Implementations.Main
{
    public class PosProductEarnRuleRepository : GenericRepository<PosProductEarnRule>, IPosProductEarnRuleRepository
    {
        public PosProductEarnRuleRepository(LoyaltyWalletDbContext context) : base(context)
        {
        }

        public async Task<IReadOnlyList<PosProductEarnRule>> GetByTenantIntegrationIdAsync(int tenantIntegrationId, CancellationToken ct = default)
        {
            return await GetAsQueryable()
                .Where(r => r.TenantIntegrationId == tenantIntegrationId && r.IsActive)
                .ToListAsync(ct);
        }
    }
}

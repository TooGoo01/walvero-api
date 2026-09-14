using Loyalty.DataAccess.Context;
using Loyalty.DataAccess.Entities.Main;
using Loyalty.DataAccess.Repositories.Abstractions.Main;
using Microsoft.EntityFrameworkCore;

namespace Loyalty.DataAccess.Repositories.Implementations.Main
{
    public class TenantIntegrationVenueRepository : GenericRepository<TenantIntegrationVenue>, ITenantIntegrationVenueRepository
    {
        public TenantIntegrationVenueRepository(LoyaltyWalletDbContext context) : base(context)
        {
        }

        public Task<TenantIntegrationVenue?> GetByTerminalGroupIdAndSourceAsync(string terminalGroupId, string source, CancellationToken ct = default)
        {
            return GetAsQueryable()
                .Include(v => v.Branch)
                .Include(v => v.TenantIntegration).ThenInclude(ti => ti.IntegrationType)
                .Include(v => v.TenantIntegration).ThenInclude(ti => ti.Tenant)
                .Include(v => v.TenantIntegration).ThenInclude(ti => ti.LoyaltyProgram)
                .FirstOrDefaultAsync(v =>
                    v.ExternalTerminalGroupId == terminalGroupId &&
                    v.IsActive &&
                    v.TenantIntegration.IsActive &&
                    v.TenantIntegration.IntegrationType.Source == source &&
                    v.TenantIntegration.IntegrationType.IsActive, ct);
        }

        public Task<List<TenantIntegrationVenue>> GetActiveByIntegrationAsync(int tenantIntegrationId, CancellationToken ct = default)
        {
            return GetAsQueryable()
                .Include(v => v.Branch)
                .Where(v => v.TenantIntegrationId == tenantIntegrationId && v.IsActive)
                .OrderBy(v => v.Id)
                .ToListAsync(ct);
        }
    }
}

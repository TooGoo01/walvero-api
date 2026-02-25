using Loyalty.DataAccess.Context;
using Loyalty.DataAccess.Entities.Main;
using Loyalty.DataAccess.Repositories.Abstractions.Main;
using Microsoft.EntityFrameworkCore;

namespace Loyalty.DataAccess.Repositories.Implementations.Main
{
    public class TenantIntegrationRepository : GenericRepository<TenantIntegration>, ITenantIntegrationRepository
    {
        public TenantIntegrationRepository(LoyaltyWalletDbContext context) : base(context)
        {
        }

        public Task<TenantIntegration?> GetByWalletTokenHashAsync(string walletTokenHash, CancellationToken ct = default)
        {
            return GetAsQueryable()
                .Include(ti => ti.IntegrationType)
                .Include(ti => ti.Tenant)
                .Include(ti => ti.LoyaltyProgram)
                .FirstOrDefaultAsync(ti => ti.WalletTokenHash == walletTokenHash && ti.IsActive && ti.IntegrationType.IsActive, ct);
        }

        public Task<TenantIntegration?> GetByVenueAndSourceAsync(string venueId, string source, CancellationToken ct = default)
        {
            return GetAsQueryable()
                .Include(ti => ti.IntegrationType)
                .Include(ti => ti.Tenant)
                .Include(ti => ti.LoyaltyProgram)
                .FirstOrDefaultAsync(ti =>
                    ti.ExternalVenueId == venueId &&
                    ti.IntegrationType.Source == source &&
                    ti.IsActive &&
                    ti.IntegrationType.IsActive, ct);
        }

        public Task<TenantIntegration?> GetActiveByTenantAndTypeAsync(int tenantId, int integrationTypeId, CancellationToken ct = default)
        {
            return GetAsQueryable()
                .Include(ti => ti.IntegrationType)
                .Include(ti => ti.LoyaltyProgram)
                .FirstOrDefaultAsync(ti =>
                    ti.TenantId == tenantId &&
                    ti.IntegrationTypeId == integrationTypeId &&
                    ti.IsActive, ct);
        }

        public Task<TenantIntegration?> GetActiveByTenantAndSourceAsync(int tenantId, string source, CancellationToken ct = default)
        {
            return GetAsQueryable()
                .Include(ti => ti.IntegrationType)
                .FirstOrDefaultAsync(ti =>
                    ti.TenantId == tenantId &&
                    ti.IntegrationType.Source == source &&
                    ti.IsActive && ti.IntegrationType.IsActive, ct);
        }

        public Task<TenantIntegration?> GetActiveByTenantAsync(int tenantId, CancellationToken ct = default)
        {
            return GetAsQueryable()
                .Include(ti => ti.IntegrationType)
                .FirstOrDefaultAsync(ti =>
                    ti.TenantId == tenantId &&
                    ti.IsActive && ti.IntegrationType.IsActive, ct);
        }
    }
}

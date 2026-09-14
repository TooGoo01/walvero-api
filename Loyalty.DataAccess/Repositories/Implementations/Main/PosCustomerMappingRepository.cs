using Loyalty.DataAccess.Context;
using Loyalty.DataAccess.Entities.Main;
using Loyalty.DataAccess.Repositories.Abstractions.Main;
using Microsoft.EntityFrameworkCore;

namespace Loyalty.DataAccess.Repositories.Implementations.Main
{
    public class PosCustomerMappingRepository : GenericRepository<PosCustomerMapping>, IPosCustomerMappingRepository
    {
        public PosCustomerMappingRepository(LoyaltyWalletDbContext context) : base(context)
        {
        }

        public async Task<int?> GetCustomerIdByExternalIdAsync(int tenantIntegrationId, string externalCustomerId, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(externalCustomerId))
                return null;

            var mapping = await GetAsQueryable()
                .Where(m => m.TenantIntegrationId == tenantIntegrationId
                            && m.ExternalCustomerId == externalCustomerId)
                .FirstOrDefaultAsync(ct);

            if (mapping == null && int.TryParse(externalCustomerId, out var parsedCustomerId))
            {
                mapping = await GetAsQueryable()
                    .Where(m => m.TenantIntegrationId == tenantIntegrationId
                                && m.CustomerId == parsedCustomerId)
                    .FirstOrDefaultAsync(ct);
            }
            return mapping?.CustomerId;
        }

        public Task<PosCustomerMapping?> GetByTenantIntegrationAndCustomerAsync(int tenantIntegrationId, int customerId, CancellationToken ct = default)
        {
            return GetAsQueryable()
                .FirstOrDefaultAsync(m => m.TenantIntegrationId == tenantIntegrationId && m.CustomerId == customerId, ct);
        }

        public Task<PosCustomerMapping?> GetByVenueAndCustomerAsync(int tenantIntegrationVenueId, int customerId, CancellationToken ct = default)
        {
            return GetAsQueryable()
                .FirstOrDefaultAsync(m => m.TenantIntegrationVenueId == tenantIntegrationVenueId && m.CustomerId == customerId, ct);
        }
    }
}

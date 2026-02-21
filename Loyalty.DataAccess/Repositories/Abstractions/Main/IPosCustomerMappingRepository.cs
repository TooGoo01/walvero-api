using Loyalty.DataAccess.Entities.Main;

namespace Loyalty.DataAccess.Repositories.Abstractions.Main
{
    public interface IPosCustomerMappingRepository : IGenericRepository<PosCustomerMapping>
    {
        Task<int?> GetCustomerIdByExternalIdAsync(int tenantIntegrationId, string externalCustomerId, CancellationToken ct = default);
        Task<PosCustomerMapping?> GetByTenantIntegrationAndCustomerAsync(int tenantIntegrationId, int customerId, CancellationToken ct = default);
    }
}

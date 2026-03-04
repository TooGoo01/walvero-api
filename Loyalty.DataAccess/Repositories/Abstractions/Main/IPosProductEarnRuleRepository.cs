using Loyalty.DataAccess.Entities.Main;

namespace Loyalty.DataAccess.Repositories.Abstractions.Main
{
    public interface IPosProductEarnRuleRepository : IGenericRepository<PosProductEarnRule>
    {
        Task<IReadOnlyList<PosProductEarnRule>> GetByTenantIntegrationIdAsync(int tenantIntegrationId, CancellationToken ct = default);
    }
}

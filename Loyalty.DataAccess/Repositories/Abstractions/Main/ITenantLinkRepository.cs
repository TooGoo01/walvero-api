using Loyalty.DataAccess.Entities.Main;

namespace Loyalty.DataAccess.Repositories.Abstractions.Main
{
    public interface ITenantLinkRepository : IGenericRepository<TenantLink>
    {
        Task<IEnumerable<TenantLink>> GetByTenantIdOrderedAsync(int tenantId);
    }
}

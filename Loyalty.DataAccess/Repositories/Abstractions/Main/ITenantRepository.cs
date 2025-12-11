using Loyalty.DataAccess.Entities.Main;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Loyalty.DataAccess.Repositories.Abstractions.Main
{
    public interface ITenantRepository : IGenericRepository<Tenant>
    {
        Task<Tenant?> GetBySubdomainAsync(string subdomain, CancellationToken cancellationToken = default);
        Task<Tenant?> GetById(int id);
        Task<bool> SubdomainExistsAsync(string subdomain, CancellationToken cancellationToken = default);
        Task<Tenant?> GetWithBrandingAsync(int tenantId, CancellationToken cancellationToken = default);
        Task<Tenant?> GetWithAllDetailsAsync(int tenantId, CancellationToken cancellationToken = default);
        Task<IEnumerable<Tenant>> GetAll();
        Task<IEnumerable<Tenant>> GetByUserId(int tenantId);
        Task<List<Tenant>> GetByIdsAsync(IEnumerable<int> tenantIds, CancellationToken ct = default);
        Task<IEnumerable<Tenant>> GetActive();
        Task<IQueryable<Tenant>> GetActiveWithUser(string? name,string? userName,int? categoryId);
    }
}

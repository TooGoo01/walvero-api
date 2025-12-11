using Loyalty.DataAccess.Entities.Main;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Loyalty.DataAccess.Repositories.Abstractions.Main
{
    public interface ITenantBrandingRepository:IGenericRepository<TenantBranding>
    {
        Task<TenantBranding?> GetByTenantIdAsync(int tenantId, CancellationToken ct = default);
        Task<TenantBranding?> GetById(int id, CancellationToken ct = default);
        Task UpsertAsync(int tenantId, TenantBranding branding, CancellationToken ct = default);
    }
}

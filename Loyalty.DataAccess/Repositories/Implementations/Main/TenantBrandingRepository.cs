using Loyalty.DataAccess.Context;
using Loyalty.DataAccess.Entities.Main;
using Loyalty.DataAccess.Repositories.Abstractions.Main;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Loyalty.DataAccess.Repositories.Implementations.Main
{
    public class TenantBrandingRepository : GenericRepository<TenantBranding>, ITenantBrandingRepository
    {
        public TenantBrandingRepository(LoyaltyWalletDbContext LoyaltyDbContext) : base(LoyaltyDbContext)
        {
        }

        public Task<TenantBranding?> GetById(int id, CancellationToken ct = default)
        => GetAsQueryable().AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);

        public Task<TenantBranding?> GetByTenantIdAsync(int tenantId, CancellationToken ct = default)
        => GetAsQueryable().AsNoTracking().FirstOrDefaultAsync(x => x.TenantId == tenantId, ct);

        public async Task UpsertAsync(int tenantId, TenantBranding branding, CancellationToken ct = default)
        {
           throw new NotImplementedException();
        }
    }
}

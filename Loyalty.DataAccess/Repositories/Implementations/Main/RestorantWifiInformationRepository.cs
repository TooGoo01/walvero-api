using Loyalty.DataAccess.Context;
using Loyalty.DataAccess.Entities.Main;
using Loyalty.DataAccess.Repositories.Abstractions.Main;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Loyalty.DataAccess.Repositories.Implementations.Main
{
    public class RestorantWifiInformationRepository : GenericRepository<RestorantWifiInformation>, IRestorantWifiInformationRepository
    {
        public RestorantWifiInformationRepository(LoyaltyWalletDbContext loyaltyDbContext) : base(loyaltyDbContext)
        {
        }

        public async Task<RestorantWifiInformation?> GetByIdWithTenantAsync(int id)
        {
            return await GetAsQueryable()
                .Include(rwi => rwi.Tenant)
                .FirstOrDefaultAsync(rwi => rwi.Id == id);
        }

        public async Task<IEnumerable<RestorantWifiInformation>> GetByTenantIdAsync(int tenantId)
        {
            return await GetAsQueryable()
                .Where(rwi => rwi.TenantId == tenantId && rwi.IsActive)
                .ToListAsync();
        }

        public async Task<IEnumerable<RestorantWifiInformation>> GetAllWithTenantAsync()
        {
            return await GetAsQueryable()
                .Include(rwi => rwi.Tenant)
                .Where(rwi => rwi.IsActive)
                .ToListAsync();
        }
    }
}


using Loyalty.DataAccess.Context;
using Loyalty.DataAccess.Entities.Main;
using Loyalty.DataAccess.Repositories.Abstractions.Main;
using Microsoft.EntityFrameworkCore;

namespace Loyalty.DataAccess.Repositories.Implementations.Main
{
    public class AppVersionRepository : GenericRepository<AppVersionConfig>, IAppVersionRepository
    {
        public AppVersionRepository(LoyaltyWalletDbContext LoyaltyDbContext) : base(LoyaltyDbContext)
        {
        }

        public async Task<AppVersionConfig?> GetActiveByPlatformAsync(string platform, CancellationToken ct = default)
        {
            return await GetAsQueryable()
            .AsNoTracking()
            .Where(x => x.IsActive && x.Platform == platform)
            .OrderByDescending(x => x.Id) // birdən çox aktiv varsa, ən yenisini götür
            .FirstOrDefaultAsync(ct);
        }

        public async Task<IEnumerable<AppVersionConfig>> GetActiveList()
        {
            return await GetAsQueryable().Where(x=>x.IsActive).ToListAsync();
        }

        public async Task<IEnumerable<AppVersionConfig>> GetAllList()
        {
            return await GetAsQueryable().ToListAsync();
        }

        public async Task<AppVersionConfig> GetById(int id)
        {
            return await GetAsQueryable()
           
            .FirstOrDefaultAsync(x=>x.Id==id);
        }

        public Task<AppVersionConfig> GetLatestByPlatform(string Platform, string CurrentVersion)
        {
            throw new NotImplementedException();
        }
    }
}

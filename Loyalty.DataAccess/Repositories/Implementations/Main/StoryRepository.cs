
using Loyalty.DataAccess.Context;
using Loyalty.DataAccess.Entities.Main;
using Loyalty.DataAccess.Models;
using Loyalty.DataAccess.Repositories.Abstractions.Main;
using Microsoft.EntityFrameworkCore;

namespace Loyalty.DataAccess.Repositories.Implementations.Main
{
    public class StoryRepository : GenericRepository<Story>, IStoryRepository
    {
        public StoryRepository(LoyaltyWalletDbContext loyaltyDbContext) : base(loyaltyDbContext)
        {
        }

        public async Task<IEnumerable<Story>> GetActiveStories()
        {
            return await GetAsQueryable()
                .Where(x => x.IsActive)
                .Include(x => x.Tenant)
                .ToListAsync();
        }

        public async Task<IEnumerable<Story>> GetAllStories()
        {
            return await GetAsQueryable()
                .Include(x => x.Tenant)
                .ToListAsync();
        }

        public async Task<Story?> GetStory(int storyId)
        {
            return await GetAsQueryable()
                .Include(x => x.Tenant)
                .FirstOrDefaultAsync(x => x.Id == storyId && x.IsActive);
        }

        public async Task<IEnumerable<Story>> GetStoriesByTenant(int tenantId)
        {
            return await GetAsQueryable()
                .Where(x => x.TenantId == tenantId && x.IsActive)
                .Include(x => x.Tenant)
                .ToListAsync();
        }

        public async Task<IReadOnlyList<HomeStoryProjection>> ListHomeStoriesAsync(CancellationToken ct = default)
        {
            return await _LoyaltyDbContext.Stories
                .AsNoTracking()
                .Where(s => s.IsActive)
                .OrderByDescending(s => s.RegDate)
                .ThenByDescending(s => s.Id)
                .Select(s => new HomeStoryProjection
                {
                    Id = s.Id,
                    AzTitle = s.AzTitle,
                    EnTitle = s.EnTitle,
                    RuTitle = s.RuTitle,
                    ImageUrl = s.ImageUrl,
                    SmallImageUrl = s.SmallImageUrl,
                    TenantId = s.TenantId
                })
                .ToListAsync(ct);
        }

        private IQueryable<Story> GetAsQueryable()
        {
            return _LoyaltyDbContext.Stories.AsQueryable();
        }
    }
}


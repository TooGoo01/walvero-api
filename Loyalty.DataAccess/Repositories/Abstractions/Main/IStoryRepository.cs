using Loyalty.DataAccess.Entities.Main;
using Loyalty.DataAccess.Models;

namespace Loyalty.DataAccess.Repositories.Abstractions.Main
{
    public interface IStoryRepository : IGenericRepository<Story>
    {
        Task<IEnumerable<Story>> GetActiveStories();
        Task<IEnumerable<Story>> GetAllStories();
        Task<Story?> GetStory(int storyId);
        Task<IEnumerable<Story>> GetStoriesByTenant(int tenantId);
        Task<IReadOnlyList<HomeStoryProjection>> ListHomeStoriesAsync(CancellationToken ct = default);
    }
}


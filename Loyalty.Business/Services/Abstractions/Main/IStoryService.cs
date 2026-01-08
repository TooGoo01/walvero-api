

using Loyalty.Business.Dtos.Post.Main;
using Loyalty.Business.Results;

namespace Loyalty.Business.Services.Abstractions.Main
{
    public interface IStoryService
    {
        Task<ServiceResult> AddStory(AddStoryDto story);
        Task<ServiceResult> GetStories();
        Task<ServiceResult> GetActiveStories();
        Task<ServiceResult> GetStory(int id);
        Task<ServiceResult> UpdateStory(AddStoryDto storyDto, int id);
        Task<ServiceResult> DeleteStory(int id);
        Task<ServiceResult> GetStoriesByTenant(int tenantId);
    }
}


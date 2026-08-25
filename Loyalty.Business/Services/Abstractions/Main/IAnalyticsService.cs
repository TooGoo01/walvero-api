using Loyalty.Business.Dtos.Get.Main;
using Loyalty.Business.Dtos.Post.Main;
using Loyalty.Business.Results;

namespace Loyalty.Business.Services.Abstractions.Main
{
    public interface IAnalyticsService
    {
        Task<ServiceResult> TrackLinkClickAsync(TrackLinkClickDto dto);

        Task<ServiceResult<LinkClickStatsDto>> GetLinkClickStatsAsync(
            int tenantId,
            DateOnly startDate,
            DateOnly endDate);
    }
}

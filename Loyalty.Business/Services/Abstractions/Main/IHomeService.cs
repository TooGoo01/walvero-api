using Loyalty.Business.Dtos.Get.Main;
using Loyalty.Business.Results;

namespace Loyalty.Business.Services.Abstractions.Main
{
    public interface IHomeService
    {
        Task<ServiceResult<List<HomeTenantDto>>> GetHomeTenantsAsync(CancellationToken ct = default);
        Task<ServiceResult<List<HomeStoryDto>>> GetHomeStoriesAsync(CancellationToken ct = default);
        Task<ServiceResult<List<HomeOfferDto>>> GetHomeOffersAsync(CancellationToken ct = default);
    }
}


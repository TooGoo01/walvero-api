using AutoMapper;
using Loyalty.Business.Dtos.Get.Main;
using Loyalty.Business.Results;
using Loyalty.Business.Services.Abstractions.Main;
using Loyalty.DataAccess.Repositories.Abstractions.Main;

namespace Loyalty.Business.Services.Implementations.Main
{
    public class HomeService : IHomeService
    {
        private readonly ITenantRepository _tenantRepository;
        private readonly IStoryRepository _storyRepository;
        private readonly IOfferRepository _offerRepository;
        private readonly IMapper _mapper;

        public HomeService(
            ITenantRepository tenantRepository,
            IStoryRepository storyRepository,
            IOfferRepository offerRepository,
            IMapper mapper)
        {
            _tenantRepository = tenantRepository;
            _storyRepository = storyRepository;
            _offerRepository = offerRepository;
            _mapper = mapper;
        }

        public async Task<ServiceResult<List<HomeTenantDto>>> GetHomeTenantsAsync(CancellationToken ct = default)
        {
            try
            {
                var projections = await _tenantRepository.ListHomeTenantsAsync(ct);

                // OPTIMIZATION: Use AutoMapper instead of manual mapping
                var tenants = _mapper.Map<List<HomeTenantDto>>(projections);

                return ServiceResult<List<HomeTenantDto>>.Ok(tenants);
            }
            catch (Exception ex)
            {
                return ServiceResult<List<HomeTenantDto>>.Fail($"Error retrieving home tenants: {ex.Message}");
            }
        }

        public async Task<ServiceResult<List<HomeStoryDto>>> GetHomeStoriesAsync(CancellationToken ct = default)
        {
            try
            {
                var projections = await _storyRepository.ListHomeStoriesAsync(ct);

                // OPTIMIZATION: Use AutoMapper instead of manual mapping
                var stories = _mapper.Map<List<HomeStoryDto>>(projections);

                return ServiceResult<List<HomeStoryDto>>.Ok(stories);
            }
            catch (Exception ex)
            {
                return ServiceResult<List<HomeStoryDto>>.Fail($"Error retrieving home stories: {ex.Message}");
            }
        }

        public async Task<ServiceResult<List<HomeOfferDto>>> GetHomeOffersAsync(CancellationToken ct = default)
        {
            try
            {
                var projections = await _offerRepository.ListHomeOffersAsync(ct);

                // OPTIMIZATION: Use AutoMapper instead of manual mapping
                var offers = _mapper.Map<List<HomeOfferDto>>(projections);

                return ServiceResult<List<HomeOfferDto>>.Ok(offers);
            }
            catch (Exception ex)
            {
                return ServiceResult<List<HomeOfferDto>>.Fail($"Error retrieving home offers: {ex.Message}");
            }
        }
    }
}


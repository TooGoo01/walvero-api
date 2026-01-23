using Loyalty.Business.Dtos.Get.Main;
using Loyalty.Business.Results;
using Loyalty.Business.Services.Abstractions.Main;
using Microsoft.AspNetCore.Mvc;
using System.Net;

namespace Loyalty.WebAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class PublicController : ControllerBase
    {
        private readonly IHomeService _homeService;
        private readonly ICountrySequenceService _countrySequenceService;

        public PublicController(
            IHomeService homeService,
            ICountrySequenceService countrySequenceService)
        {
            _homeService = homeService;
            _countrySequenceService = countrySequenceService;
        }

        /// <summary>
        /// Get home page tenants (IsActive and ShowOnHome) with optional location filtering
        /// </summary>
        [HttpGet("home/tenants")]
        [ProducesResponseType(typeof(ServiceResult<List<HomeTenantDto>>), (int)HttpStatusCode.OK)]
        [ResponseCache(Duration = 120, VaryByQueryKeys = new[] { "countryId", "cityId" })]
        public async Task<ActionResult<ServiceResult<List<HomeTenantDto>>>> GetHomeTenants(
            [FromQuery] int? countryId,
            [FromQuery] int? cityId,
            CancellationToken ct)
        {
            var result = await _homeService.GetHomeTenantsAsync(countryId, cityId, ct);
            
            if (result.Success)
            {
                return Ok(result);
            }

            return BadRequest(result);
        }

        /// <summary>
        /// Get home page stories (active, ordered by newest)
        /// </summary>
        [HttpGet("home/stories")]
        [ProducesResponseType(typeof(ServiceResult<List<HomeStoryDto>>), (int)HttpStatusCode.OK)]
        public async Task<ActionResult<ServiceResult<List<HomeStoryDto>>>> GetHomeStories(CancellationToken ct)
        {
            var result = await _homeService.GetHomeStoriesAsync(ct);
            
            if (result.Success)
            {
                return Ok(result);
            }

            return BadRequest(result);
        }

        /// <summary>
        /// Get home page offers (active, ordered by newest)
        /// </summary>
        [HttpGet("home/offers")]
        [ProducesResponseType(typeof(ServiceResult<List<HomeOfferDto>>), (int)HttpStatusCode.OK)]
        public async Task<ActionResult<ServiceResult<List<HomeOfferDto>>>> GetHomeOffers(CancellationToken ct)
        {
            var result = await _homeService.GetHomeOffersAsync(ct);
            
            if (result.Success)
            {
                return Ok(result);
            }

            return BadRequest(result);
        }

        /// <summary>
        /// Get active country sequences
        /// </summary>
        [HttpGet("countries")]
        [ProducesResponseType(typeof(ServiceResult<List<GetCountrySequenceDto>>), (int)HttpStatusCode.OK)]
        public async Task<ActionResult<ServiceResult<List<GetCountrySequenceDto>>>> GetActiveCountries(CancellationToken ct)
        {
            var result = await _countrySequenceService.GetActiveCountriesAsync(ct);
            
            if (result.Success)
            {
                return Ok(result);
            }

            return BadRequest(result);
        }
    }
}


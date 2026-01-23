using Loyalty.Business.Dtos.Get.Main;
using Loyalty.Business.Results;
using Loyalty.Business.Services.Abstractions.Main;
using Microsoft.AspNetCore.Mvc;
using System.Net;

namespace Loyalty.WebAPI.Controllers
{
    /// <summary>
    /// Location management endpoints (Countries and Cities)
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    public class LocationController : ControllerBase
    {
        private readonly ICountryService _countryService;
        private readonly ICityService _cityService;

        public LocationController(
            ICountryService countryService,
            ICityService cityService)
        {
            _countryService = countryService;
            _cityService = cityService;
        }

        /// <summary>
        /// Get all active countries with their cities (multi-language support)
        /// </summary>
        [HttpGet("countries")]
        [ProducesResponseType(typeof(ServiceResult<List<GetCountryWithCitiesDto>>), (int)HttpStatusCode.OK)]
        [ResponseCache(Duration = 300)] // 5 minutes cache - countries change rarely
        public async Task<ActionResult<ServiceResult>> GetCountries()
        {
            var result = await _countryService.GetAllCountriesWithCities();
            
            if (result.Success)
            {
                return Ok(result);
            }

            return BadRequest(result);
        }

        /// <summary>
        /// Get a specific country by ID (without cities)
        /// </summary>
        [HttpGet("countries/{id}")]
        [ProducesResponseType(typeof(ServiceResult<GetCountryDto>), (int)HttpStatusCode.OK)]
        [ResponseCache(Duration = 300)]
        public async Task<ActionResult<ServiceResult>> GetCountry(int id)
        {
            var result = await _countryService.GetCountry(id);
            
            if (result.Success)
            {
                return Ok(result);
            }

            return NotFound(result);
        }

        /// <summary>
        /// Get a specific country by ID with all its cities (multi-language)
        /// </summary>
        [HttpGet("countries/{id}/with-cities")]
        [ProducesResponseType(typeof(ServiceResult<GetCountryWithCitiesDto>), (int)HttpStatusCode.OK)]
        [ResponseCache(Duration = 300, VaryByQueryKeys = new[] { "id" })]
        public async Task<ActionResult<ServiceResult>> GetCountryWithCities(int id)
        {
            var result = await _countryService.GetCountryWithCities(id);
            
            if (result.Success)
            {
                return Ok(result);
            }

            return NotFound(result);
        }

        /// <summary>
        /// Get all cities for a specific country with multi-language support
        /// </summary>
        [HttpGet("countries/{countryId}/cities")]
        [ProducesResponseType(typeof(ServiceResult<List<GetCityDto>>), (int)HttpStatusCode.OK)]
        [ResponseCache(Duration = 300, VaryByQueryKeys = new[] { "countryId" })]
        public async Task<ActionResult<ServiceResult>> GetCitiesByCountry(int countryId)
        {
            var result = await _cityService.GetCitiesByCountry(countryId);
            
            if (result.Success)
            {
                return Ok(result);
            }

            return BadRequest(result);
        }

        /// <summary>
        /// Get all cities (all countries)
        /// </summary>
        [HttpGet("cities")]
        [ProducesResponseType(typeof(ServiceResult<List<GetCityDto>>), (int)HttpStatusCode.OK)]
        [ResponseCache(Duration = 300)]
        public async Task<ActionResult<ServiceResult>> GetAllCities()
        {
            var result = await _cityService.GetAllCities();
            
            if (result.Success)
            {
                return Ok(result);
            }

            return BadRequest(result);
        }

        /// <summary>
        /// Get a specific city by ID
        /// </summary>
        [HttpGet("cities/{id}")]
        [ProducesResponseType(typeof(ServiceResult<GetCityDto>), (int)HttpStatusCode.OK)]
        [ResponseCache(Duration = 300)]
        public async Task<ActionResult<ServiceResult>> GetCity(int id)
        {
            var result = await _cityService.GetCity(id);
            
            if (result.Success)
            {
                return Ok(result);
            }

            return NotFound(result);
        }
    }
}

using Loyalty.Business.Dtos.Get.Main;
using Loyalty.Business.Dtos.Post.Main;
using Loyalty.Business.Results;
using Loyalty.Business.Services.Abstractions.Main;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Net;

namespace Loyalty.WebAPI.Controllers
{
    [Route("api/[controller]/[action]")]
    [ApiController]
    public class TenantController : ControllerBase
    {
        private readonly ITenantService _tenantService;
        private readonly ICountryService _countryService;
        private readonly ICityService _cityService;
        private readonly ITimeZoneService _timeZoneService;
        public TenantController(ITenantService tenantService, ITimeZoneService timeZoneService, ICityService cityService, ICountryService countryService)
        {
            _tenantService = tenantService;
            _timeZoneService = timeZoneService;
            _cityService = cityService;
            _countryService = countryService;
        }

        /// <summary>
        /// Get active tenants with optional filtering by category, location, name, and user
        /// </summary>
        [HttpGet]
        [ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.OK)]
        [ResponseCache(
            Duration = 60, 
            VaryByQueryKeys = new[] { "currentPage", "pageSize", "categoryId", "countryId", "cityId", "name", "userName" })]
        public async Task<ActionResult<ServiceResult>> GetTenants(
            [FromQuery] TenantSearchDto? model, 
            int currentPage = 1, 
            int pageSize = 10)
        {
            return Ok(await _tenantService.GetActiveUser(currentPage, pageSize, model));
        }
        [HttpGet]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Policy = "SuperAdminOnly")]
        [ProducesResponseType(typeof(ServiceResult<List<GetTenantDto>>), (int)HttpStatusCode.OK)]
        [ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.NotFound)]
        [ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.Unauthorized)]
        [ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.BadRequest)]
        public async Task<IActionResult> List()
        {
            var result = await _tenantService.GetAll();
            return Ok(result);
        }

        [HttpPost]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Policy = "SuperAdminOnly")]
        [ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.OK)]
        [ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.NotFound)]
        [ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.Unauthorized)]
        [ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.BadRequest)]
        public async Task<IActionResult> Add(AddTenantDto addTenantDto)
        {
            if (!ModelState.IsValid)
            {
              

                foreach (var error in ModelState.Values.SelectMany(modelState => modelState.Errors))
                {
                    ModelState.AddModelError("", error.ErrorMessage);
                }
                return BadRequest(ModelState);
            }
            var result = await _tenantService.Add(addTenantDto);
            if (result.Success)
                return Ok(result);

            return BadRequest(result);
        }

        [HttpPost]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Policy = "SuperAdminOnly")]
        [ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.OK)]
        [ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.NotFound)]
        [ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.Unauthorized)]
        [ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.BadRequest)]
        public async Task<IActionResult> Update(UpdateTenantDto updateTenantDto)
        {
            if (!ModelState.IsValid)
            {
                var tenant = await _tenantService.GetById(updateTenantDto.Id);
                var tenantDto = tenant.Data as GetTenantDto;
                if (tenantDto == null)
                {
                    return NotFound();
                }

              

              
                foreach (var error in ModelState.Values.SelectMany(modelState => modelState.Errors))
                {
                    ModelState.AddModelError("", error.ErrorMessage);
                }
                return BadRequest(ModelState);
            }
            var result = await _tenantService.Update(updateTenantDto);
            if (result.Success)
                return Ok(result);

          

         
            return BadRequest(result);
        }

       
    }
}

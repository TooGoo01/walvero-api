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
    public class TenantBrandingController : ControllerBase
    {
        private readonly ITenantBrandingService _tenantBrandingService;
        private readonly ITenantProvider _tenantProvider;
        public TenantBrandingController(ITenantBrandingService tenantBrandingService, ITenantProvider tenantProvider)
        {
            _tenantBrandingService = tenantBrandingService;
            _tenantProvider = tenantProvider;
        }

        /// <summary>Returns branding for the currently authenticated tenant.</summary>
        [HttpGet]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Policy = "TenantAdminOrModerator")]
        [ProducesResponseType(typeof(ServiceResult<GetTenantBrandingDto>), (int)HttpStatusCode.OK)]
        [ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.Unauthorized)]
        public async Task<IActionResult> Get()
        {
            var tenantId = _tenantProvider.CurrentTenantId;
            if (!tenantId.HasValue) return Unauthorized(new ServiceResult(false, "Tenant not found in token"));
            var result = await _tenantBrandingService.GetByTenantId(tenantId.Value);
            return Ok(result);
        }

        /// <summary>Updates branding for the currently authenticated tenant.</summary>
        [HttpPost]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Policy = "TenantAdminOnly")]
        [ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.OK)]
        [ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.BadRequest)]
        [ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.Unauthorized)]
        public async Task<IActionResult> UpdateMy([FromForm] AddTenantBrandingDto dto, CancellationToken ct)
        {
            var tenantId = _tenantProvider.CurrentTenantId;
            if (!tenantId.HasValue) return Unauthorized(new ServiceResult(false, "Tenant not found in token"));
            dto.TenantId = tenantId.Value;
            var result = await _tenantBrandingService.AddOrUpdateAsync(dto, ct);
            if (result.Success) return Ok(result);
            return BadRequest(result);
        }

        [HttpGet]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Policy = "SuperAdminOnly")]
        [ProducesResponseType(typeof(ServiceResult<List<GetTenantBrandingDto>>), (int)HttpStatusCode.OK)]
        [ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.NotFound)]
        [ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.Unauthorized)]
        [ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.BadRequest)]
        public async Task<IActionResult> List()
        {
            var result = await _tenantBrandingService.GetAll();
            return Ok(result);
        }

        [HttpPost]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Policy = "SuperAdminOnly")]
        [ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.OK)]
        [ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.NotFound)]
        [ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.Unauthorized)]
        [ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.BadRequest)]
        public async Task<IActionResult> Add([FromForm] AddTenantBrandingDto addTenantDto, CancellationToken ct)
        {
            if (!ModelState.IsValid)
            {
                foreach (var error in ModelState.Values.SelectMany(modelState => modelState.Errors))
                {
                    ModelState.AddModelError("", error.ErrorMessage);
                }
                return BadRequest(ModelState);
            }
            var result = await _tenantBrandingService.AddOrUpdateAsync(addTenantDto, ct);
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
        public async Task<IActionResult> Update(AddTenantBrandingDto updateTenantDto, int Id)
        {
            if (!ModelState.IsValid)
            {
                foreach (var error in ModelState.Values.SelectMany(modelState => modelState.Errors))
                {
                    ModelState.AddModelError("", error.ErrorMessage);
                }
                return Ok(ModelState);
            }
            var result = await _tenantBrandingService.AddOrUpdateAsync(updateTenantDto);
            if (result.Success)
                return Ok(result);

            return BadRequest(result);
        }

        [HttpDelete]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Policy = "SuperAdminOnly")]
        [ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.OK)]
        [ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.NotFound)]
        [ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.Unauthorized)]
        [ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.BadRequest)]
        public async Task<IActionResult> Delete(int id)
        {
            var result = await _tenantBrandingService.Delete(id);
            if (result.Success)
                return Ok(result);
            return BadRequest(result);
        }

    }
}

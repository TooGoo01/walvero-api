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
    [Authorize(
    AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme,
    Policy = "TenantAdminOrModerator")]
    public class LoyaltyProgramController : ControllerBase
    {
        private readonly ILoyaltyProgramService _loyaltyProgramService;
        private readonly ITenantProvider _tenantProvider;

        public LoyaltyProgramController(ITenantProvider tenantProvider, ILoyaltyProgramService loyaltyProgramService)
        {
            _tenantProvider = tenantProvider;
            _loyaltyProgramService = loyaltyProgramService;
        }
        [HttpGet]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Policy = "SuperAdminOnly")]
        [ProducesResponseType(typeof(ServiceResult<List<GetLoyaltyProgramDto>>), (int)HttpStatusCode.OK)]
        [ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.NotFound)]
        [ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.Unauthorized)]
        [ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.BadRequest)]
        public async Task<IActionResult> List()
        {
            var result = await _loyaltyProgramService.GetAll();
            return Ok(result);
        }

        /// <summary>Cari tenant-ın aktiv proqramları (tenant admin — məs. ambassador dialoqu üçün).</summary>
        [HttpGet]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Policy = "TenantAdminOrModerator")]
        [ProducesResponseType(typeof(ServiceResult<List<GetLoyaltyProgramDto>>), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> MyList()
        {
            var tenantId = _tenantProvider.CurrentTenantId;
            if (!tenantId.HasValue)
                return Ok(new ServiceResult(false, "Tenant not found."));
            var result = await _loyaltyProgramService.GetActiveByTenantAsync(tenantId.Value);
            return Ok(result);
        }
        [HttpPost]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Policy = "SuperAdminOnly")]
        [ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.OK)]
        [ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.NotFound)]
        [ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.Unauthorized)]
        [ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.BadRequest)]
        public async Task<IActionResult> Add([FromForm] AddLoyaltyProgramDto addTenantDto, CancellationToken ct)
        {
            if (!ModelState.IsValid)
            {

                foreach (var error in ModelState.Values.SelectMany(modelState => modelState.Errors))
                {
                    ModelState.AddModelError("", error.ErrorMessage);
                }
                return BadRequest(ModelState);
            }
            var result = await _loyaltyProgramService.AddOrUpdateAsync(addTenantDto, ct);
            if (result.Success)
                return Ok(result);

            //ModelState.AddModelError("", "Butun hisselerin tam doldurulduqundan emin olun!");
            return BadRequest(result);
        }
        [HttpPost]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Policy = "SuperAdminOnly")]
        [ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.OK)]
        [ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.NotFound)]
        [ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.Unauthorized)]
        [ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.BadRequest)]
        public async Task<IActionResult> Update([FromForm] AddLoyaltyProgramDto addTenantDto, int id, CancellationToken ct)
        {
            if (!ModelState.IsValid)
            {

                foreach (var error in ModelState.Values.SelectMany(modelState => modelState.Errors))
                {
                    ModelState.AddModelError("", error.ErrorMessage);
                }
                return BadRequest(ModelState);
            }
            var result = await _loyaltyProgramService.AddOrUpdateAsync(addTenantDto, ct);
            if (result.Success)
                return Ok(result);

            //ModelState.AddModelError("", "Butun hisselerin tam doldurulduqundan emin olun!");
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
            var result = await _loyaltyProgramService.Delete(id);
            if (result.Success)
                return Ok(result);
            return BadRequest(result);
        }

        [HttpGet]
        [ProducesResponseType(typeof(ServiceResult<GetLoyaltyProgramDto>), (int)HttpStatusCode.OK)]
        [ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.NotFound)]
        public async Task<IActionResult> Get(int id)
        {
            var result = await _loyaltyProgramService.Get(id);
            if (result.Success)
                return Ok(result);
            return NotFound(result);
        }

        [HttpPost]
        [ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.OK)]
        [ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.BadRequest)]
        public async Task<IActionResult> UpdateHappyHours(int id, [FromBody] UpdateHappyHoursDto dto, CancellationToken ct)
        {
            var result = await _loyaltyProgramService.UpdateHappyHoursAsync(id, dto, ct);
            if (result.Success)
                return Ok(result);
            return BadRequest(result);
        }
    }
}

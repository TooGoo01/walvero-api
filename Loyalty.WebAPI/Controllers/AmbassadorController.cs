using Loyalty.Business.Dtos.Post.Main;
using Loyalty.Business.Results;
using Loyalty.Business.Services.Abstractions.Main;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Net;

namespace Loyalty.WebAPI.Controllers
{
    [Route("api/[controller]/[action]")]
    [ApiController]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public class AmbassadorController : ControllerBase
    {
        private readonly IAmbassadorService _ambassadorService;
        private readonly ITenantProvider _tenantProvider;

        public AmbassadorController(IAmbassadorService ambassadorService, ITenantProvider tenantProvider)
        {
            _ambassadorService = ambassadorService;
            _tenantProvider = tenantProvider;
        }

        /// <summary>Tenant admin ambassador (promoter) müştəri yaradır. TenantId cari admin-dən.</summary>
        [HttpPost]
        [Authorize(Policy = "TenantAdminOrModerator")]
        [ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> Create([FromBody] CreateAmbassadorDto model, CancellationToken ct)
        {
            if (!ModelState.IsValid)
                return BadRequest(new ServiceResult(false, "Invalid request data"));

            var tenantId = _tenantProvider.CurrentTenantId;
            if (!tenantId.HasValue)
                return BadRequest(new ServiceResult(false, "Tenant not found."));

            var userId = _tenantProvider.CurrentUserId ?? "";

            try
            {
                var result = await _ambassadorService.CreateAmbassadorAsync(tenantId.Value, userId, model, ct);
                return result.Success ? Ok(result) : BadRequest(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ServiceResult(false, $"Server error: {ex.Message}"));
            }
        }

        /// <summary>Ambassador kartına manual xal əlavə et. Wallet push + POS forward tetiklenir.</summary>
        [HttpPost]
        [Authorize(Policy = "TenantAdminOrModerator")]
        [ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> AddPoints([FromBody] CreditPointsDto model, CancellationToken ct)
        {
            if (!ModelState.IsValid)
                return BadRequest(new ServiceResult(false, "Invalid request data"));

            var tenantId = _tenantProvider.CurrentTenantId;
            if (!tenantId.HasValue)
                return BadRequest(new ServiceResult(false, "Tenant not found."));

            try
            {
                var result = await _ambassadorService.AddPointsAsync(tenantId.Value, model, ct);
                return result.Success ? Ok(result) : BadRequest(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ServiceResult(false, $"Server error: {ex.Message}"));
            }
        }
    }
}

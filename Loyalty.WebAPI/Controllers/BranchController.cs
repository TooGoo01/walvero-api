using Loyalty.Business.Dtos.Post.Branch;
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
    [Authorize(
        AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme,
        Policy = "TenantAdminOrModerator")]
    public class BranchController : ControllerBase
    {
        private readonly IBranchService _branchService;
        private readonly ITenantProvider _tenantProvider;

        public BranchController(IBranchService branchService, ITenantProvider tenantProvider)
        {
            _branchService = branchService;
            _tenantProvider = tenantProvider;
        }

        [HttpGet]
        [ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> List(CancellationToken ct)
        {
            var tenantId = _tenantProvider.CurrentTenantId ?? 0;
            var result = await _branchService.ListAsync(tenantId, ct);
            if (!result.Success)
                return BadRequest(new { message = result.Message });
            return Ok(result.Data);
        }

        [HttpGet]
        [ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> GetById([FromQuery] int id, CancellationToken ct)
        {
            var result = await _branchService.GetByIdAsync(id, ct);
            if (!result.Success)
                return BadRequest(new { message = result.Message });
            return Ok(result.Data);
        }

        [HttpPost]
        [ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> Create([FromBody] CreateBranchDto dto, CancellationToken ct)
        {
            var tenantId = _tenantProvider.CurrentTenantId ?? 0;
            var result = await _branchService.CreateAsync(tenantId, dto, ct);
            if (!result.Success)
                return BadRequest(new { message = result.Message });
            return Ok(result.Data);
        }

        [HttpPost]
        [ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> Update([FromQuery] int id, [FromBody] UpdateBranchDto dto, CancellationToken ct)
        {
            var result = await _branchService.UpdateAsync(id, dto, ct);
            if (!result.Success)
                return BadRequest(new { message = result.Message });
            return Ok(result.Data);
        }

        [HttpPost]
        [ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> Delete([FromQuery] int id, CancellationToken ct)
        {
            var result = await _branchService.DeleteAsync(id, ct);
            if (!result.Success)
                return BadRequest(new { message = result.Message });
            return Ok(result.Data);
        }

        [HttpGet]
        [Authorize(
            AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme,
            Policy = "CustomerOrTenantAdmin")]
        [ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> GetNearby(
            [FromQuery] int tenantId,
            [FromQuery] double lat,
            [FromQuery] double lng,
            [FromQuery] double radiusKm = 10,
            CancellationToken ct = default)
        {
            var result = await _branchService.GetNearbyAsync(tenantId, lat, lng, radiusKm, ct);
            if (!result.Success)
                return BadRequest(new { message = result.Message });
            return Ok(result.Data);
        }

        [HttpGet]
        [ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> GetStats([FromQuery] int branchId, CancellationToken ct)
        {
            var result = await _branchService.GetStatsAsync(branchId, ct);
            if (!result.Success)
                return BadRequest(new { message = result.Message });
            return Ok(result.Data);
        }
    }
}

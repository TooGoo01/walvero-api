using Loyalty.Business.Services.Abstractions.Main;
using Loyalty.WebAPI.Tenancy;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Loyalty.WebAPI.Controllers
{
    [Route("api/rewards")]
    [ApiController]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Policy = "TenantAdminOrModerator")]
    public class RewardController : ControllerBase
    {
        private readonly IRewardCrudService _service;
        private readonly ITenantProvider _tenantProvider;

        public RewardController(IRewardCrudService service, ITenantProvider tenantProvider)
        {
            _service = service;
            _tenantProvider = tenantProvider;
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] RewardCreateDto dto, CancellationToken ct)
        {
            var tid = _tenantProvider.CurrentTenantId;
            var result = await _service.CreateAsync(dto, tid, ct);
            if (!result.Success) return BadRequest(result.Message);
            return Ok(result.Data);
        }

        [HttpGet("by-program/{loyaltyProgramId:int}")]
        public async Task<IActionResult> ListByProgram(int loyaltyProgramId, CancellationToken ct)
        {
            var tid = _tenantProvider.CurrentTenantId;
            var result = await _service.ListByProgramAsync(loyaltyProgramId, tid, ct);
            if (!result.Success) return BadRequest(result.Message);
            return Ok(result.Data);
        }

        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetById(int id, CancellationToken ct)
        {
            var tid = _tenantProvider.CurrentTenantId;
            var result = await _service.GetByIdAsync(id, tid, ct);
            if (result.Data == null) return NotFound();
            return Ok(result.Data);
        }

        [HttpPut("{id:int}")]
        public async Task<IActionResult> Update(int id, [FromBody] RewardUpdateDto dto, CancellationToken ct)
        {
            var tid = _tenantProvider.CurrentTenantId;
            var result = await _service.UpdateAsync(id, dto, tid, ct);
            if (!result.Success) return BadRequest(result.Message);
            return Ok();
        }

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id, CancellationToken ct)
        {
            var tid = _tenantProvider.CurrentTenantId;
            var result = await _service.DeleteAsync(id, tid, ct);
            if (!result.Success) return BadRequest(result.Message);
            return Ok();
        }
    }
}

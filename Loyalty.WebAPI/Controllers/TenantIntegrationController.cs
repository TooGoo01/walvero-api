using Loyalty.Business.Services.Abstractions.Main;
using Loyalty.DataAccess.Repositories.Abstractions.Main;
using Loyalty.WebAPI.Tenancy;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Loyalty.WebAPI.Controllers
{
    [Route("api/tenant-integrations")]
    [ApiController]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public class TenantIntegrationController : ControllerBase
    {
        private readonly ITenantIntegrationService _service;
        private readonly IIntegrationTypeRepository _typeRepo;
        private readonly ITenantProvider _tenantProvider;

        public TenantIntegrationController(
            ITenantIntegrationService service,
            IIntegrationTypeRepository typeRepo,
            ITenantProvider tenantProvider)
        {
            _service = service;
            _typeRepo = typeRepo;
            _tenantProvider = tenantProvider;
        }

        [HttpGet("types")]
        public async Task<IActionResult> GetIntegrationTypes(CancellationToken ct)
        {
            var types = await _typeRepo.GetAllActiveAsync(ct);
            return Ok(types.Select(t => new { t.Id, t.Name, t.Source }));
        }

        [HttpPost]
        [Authorize(Policy = "TenantAdminOrModerator")]
        public async Task<IActionResult> Create([FromBody] TenantIntegrationCreateDto dto, CancellationToken ct)
        {
            var tenantId = _tenantProvider.CurrentTenantId ?? dto.TenantId;
            if (tenantId <= 0) return BadRequest("TenantId required.");
            dto.TenantId = tenantId;

            var result = await _service.CreateAsync(dto, ct);
            if (!result.Success) return BadRequest(result.Message);
            return Ok(result.Data);
        }

        [HttpGet]
        [Authorize(Policy = "TenantAdminOrModerator")]
        public async Task<IActionResult> List([FromQuery] int? tenantId, CancellationToken ct)
        {
            var tid = tenantId ?? _tenantProvider.CurrentTenantId;
            if (!tid.HasValue || tid <= 0) return BadRequest("TenantId required.");
            var result = await _service.ListByTenantAsync(tid.Value, ct);
            return Ok(result.Data);
        }

        [HttpGet("{id}")]
        [Authorize(Policy = "TenantAdminOrModerator")]
        public async Task<IActionResult> GetById(int id, [FromQuery] int? tenantId, CancellationToken ct)
        {
            var tid = tenantId ?? _tenantProvider.CurrentTenantId;
            var result = await _service.GetByIdAsync(id, tid, ct);
            if (result.Data == null) return NotFound();
            return Ok(result.Data);
        }

        [HttpPut("{id}")]
        [Authorize(Policy = "TenantAdminOrModerator")]
        public async Task<IActionResult> Update(int id, [FromBody] TenantIntegrationUpdateDto dto, [FromQuery] int? tenantId, CancellationToken ct)
        {
            var tid = tenantId ?? _tenantProvider.CurrentTenantId;
            var result = await _service.UpdateAsync(id, dto, tid, ct);
            if (!result.Success) return BadRequest(result.Message);
            return Ok();
        }

        [HttpDelete("{id}")]
        [Authorize(Policy = "TenantAdminOrModerator")]
        public async Task<IActionResult> Delete(int id, [FromQuery] int? tenantId, CancellationToken ct)
        {
            var tid = tenantId ?? _tenantProvider.CurrentTenantId;
            var result = await _service.DeleteAsync(id, tid, ct);
            if (!result.Success) return BadRequest(result.Message);
            return Ok();
        }

        [HttpPost("{id}/regenerate-wallet-token")]
        [Authorize(Policy = "TenantAdminOrModerator")]
        public async Task<IActionResult> RegenerateWalletToken(int id, [FromQuery] int? tenantId, CancellationToken ct)
        {
            var tid = tenantId ?? _tenantProvider.CurrentTenantId;
            var result = await _service.RegenerateWalletTokenAsync(id, tid, ct);
            if (!result.Success) return BadRequest(result.Message);
            return Ok(new { walletToken = result.Data });
        }
    }
}

using Loyalty.Business.Services.Abstractions.Main;
using Loyalty.WebAPI.Tenancy;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Loyalty.WebAPI.Controllers
{
    /// <summary>
    /// Bonus qazandırmayan məhsul / iiko qrupu / kateqoriya (yalnız super admin) + iiko nomenklatura surəti.
    /// </summary>
    [Route("api/tenant-integrations/{tenantIntegrationId:int}")]
    [ApiController]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Policy = "SuperAdminOnly")]
    public class PosEarnExclusionController : ControllerBase
    {
        private readonly IPosEarnExclusionService _service;
        private readonly ITenantProvider _tenantProvider;

        public PosEarnExclusionController(IPosEarnExclusionService service, ITenantProvider tenantProvider)
        {
            _service = service;
            _tenantProvider = tenantProvider;
        }

        private int CurrentUserId => int.TryParse(_tenantProvider.CurrentUserId, out var uid) ? uid : 1;

        [HttpGet("earn-exclusions")]
        public async Task<IActionResult> List(int tenantIntegrationId, CancellationToken ct)
        {
            var r = await _service.ListAsync(tenantIntegrationId, ct);
            return r.Success ? Ok(r.Data) : BadRequest(r.Message);
        }

        /// <summary>Bir və ya bir neçə qayda əlavə edir; (növ, ID) artıq varsa flag-ları yenilənir.</summary>
        [HttpPost("earn-exclusions")]
        public async Task<IActionResult> Upsert(int tenantIntegrationId, [FromBody] List<PosEarnExclusionCreateDto> items, CancellationToken ct)
        {
            var r = await _service.UpsertAsync(tenantIntegrationId, items, CurrentUserId, ct);
            return r.Success ? Ok(r.Data) : BadRequest(r.Message);
        }

        [HttpPut("earn-exclusions/{id:int}")]
        public async Task<IActionResult> Update(int tenantIntegrationId, int id, [FromBody] PosEarnExclusionUpdateDto dto, CancellationToken ct)
        {
            var r = await _service.UpdateAsync(tenantIntegrationId, id, dto, CurrentUserId, ct);
            return r.Success ? Ok() : BadRequest(r.Message);
        }

        [HttpDelete("earn-exclusions/{id:int}")]
        public async Task<IActionResult> Delete(int tenantIntegrationId, int id, CancellationToken ct)
        {
            var r = await _service.DeleteAsync(tenantIntegrationId, id, ct);
            return r.Success ? Ok() : BadRequest(r.Message);
        }

        /// <summary>Sifariş nümunəsi üzrə bonus hesabı (DB-yə yazmır).</summary>
        [HttpPost("earn-exclusions/simulate")]
        public async Task<IActionResult> Simulate(int tenantIntegrationId, [FromBody] EarnExclusionSimulateRequestDto request, CancellationToken ct)
        {
            var r = await _service.SimulateAsync(tenantIntegrationId, request, ct);
            return r.Success ? Ok(r.Data) : BadRequest(r.Message);
        }

        [HttpGet("iiko/nomenclature")]
        public async Task<IActionResult> Nomenclature(int tenantIntegrationId, CancellationToken ct)
        {
            var r = await _service.GetNomenclatureAsync(tenantIntegrationId, ct);
            return r.Success ? Ok(r.Data) : BadRequest(r.Message);
        }

        [HttpGet("iiko/nomenclature/products")]
        public async Task<IActionResult> Products(
            int tenantIntegrationId, [FromQuery] string? q, [FromQuery] string? groupId, [FromQuery] string? categoryId,
            [FromQuery] int take = 50, CancellationToken ct = default)
        {
            var r = await _service.SearchProductsAsync(tenantIntegrationId, q, groupId, categoryId, take, ct);
            return r.Success ? Ok(r.Data) : BadRequest(r.Message);
        }

        [HttpPost("iiko/nomenclature/refresh")]
        public async Task<IActionResult> Refresh(int tenantIntegrationId, CancellationToken ct)
        {
            var r = await _service.RefreshNomenclatureAsync(tenantIntegrationId, ct);
            return r.Success ? Ok(r.Data) : BadRequest(r.Message);
        }
    }
}

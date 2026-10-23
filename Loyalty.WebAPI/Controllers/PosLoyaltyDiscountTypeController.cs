using Loyalty.Business.Services.Abstractions.Main;
using Loyalty.WebAPI.Tenancy;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Loyalty.WebAPI.Controllers
{
    [Route("api/tenant-integrations/{tenantIntegrationId:int}/loyalty-discount-types")]
    [ApiController]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Policy = "SuperAdminOnly")]
    public class PosLoyaltyDiscountTypeController : ControllerBase
    {
        private readonly IPosLoyaltyDiscountTypeService _service;
        private readonly IIikoCatalogService _iikoCatalog;
        private readonly ITenantProvider _tenantProvider;

        public PosLoyaltyDiscountTypeController(
            IPosLoyaltyDiscountTypeService service, IIikoCatalogService iikoCatalog, ITenantProvider tenantProvider)
        {
            _service = service;
            _iikoCatalog = iikoCatalog;
            _tenantProvider = tenantProvider;
        }

        /// <summary>
        /// Seçim üçün kassa endirimləri: iiko endirim siyahısı + sifarişlərdə görünənlər (bağlı olanlar işarəli,
        /// adında "Walvero" olan təklif). Super admin tenantId ötürür.
        /// </summary>
        [HttpGet("candidates")]
        public async Task<IActionResult> Candidates(int tenantIntegrationId, [FromQuery] int? tenantId, [FromQuery] bool refresh = false, CancellationToken ct = default)
        {
            var tid = this.ScopedTenantId(tenantId, _tenantProvider);
            if (!tid.HasValue || tid <= 0) return BadRequest("TenantId required.");
            var result = await _iikoCatalog.GetDiscountCandidatesAsync(tid.Value, tenantIntegrationId, refresh, ct);
            if (!result.Success) return BadRequest(result.Message);
            return Ok(result.Data);
        }

        [HttpPost]
        public async Task<IActionResult> Create(int tenantIntegrationId, [FromBody] PosLoyaltyDiscountTypeCreateDto dto, [FromQuery] int? tenantId, CancellationToken ct)
        {
            dto.TenantIntegrationId = tenantIntegrationId;
            var tid = this.ScopedTenantId(tenantId, _tenantProvider);
            var result = await _service.CreateAsync(dto, tid, ct);
            if (!result.Success) return BadRequest(result.Message);
            return Ok(result.Data);
        }

        [HttpGet]
        public async Task<IActionResult> List(int tenantIntegrationId, [FromQuery] int? tenantId, CancellationToken ct)
        {
            var tid = this.ScopedTenantId(tenantId, _tenantProvider);
            var result = await _service.ListByTenantIntegrationAsync(tenantIntegrationId, tid, ct);
            if (!result.Success) return BadRequest(result.Message);
            return Ok(result.Data);
        }

        [HttpPut("{id:int}")]
        public async Task<IActionResult> Update(int tenantIntegrationId, int id, [FromBody] PosLoyaltyDiscountTypeUpdateDto dto, [FromQuery] int? tenantId, CancellationToken ct)
        {
            var tid = this.ScopedTenantId(tenantId, _tenantProvider);
            var result = await _service.UpdateAsync(id, dto, tid, ct);
            if (!result.Success) return BadRequest(result.Message);
            return Ok();
        }

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int tenantIntegrationId, int id, [FromQuery] int? tenantId, CancellationToken ct)
        {
            var tid = this.ScopedTenantId(tenantId, _tenantProvider);
            var result = await _service.DeleteAsync(id, tid, ct);
            if (!result.Success) return BadRequest(result.Message);
            return Ok();
        }
    }
}

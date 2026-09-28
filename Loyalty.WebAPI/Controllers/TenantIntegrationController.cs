using Loyalty.Business.Dtos.Post.Main;
using Loyalty.Business.Services.Abstractions.Main;
using Loyalty.DataAccess.Entities.Main;
using Loyalty.DataAccess.Repositories.Abstractions.Main;
using Loyalty.DataAccess.UnitOfWorks;
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
        private readonly IUnitOfWork _uow;

        public TenantIntegrationController(
            ITenantIntegrationService service,
            IIntegrationTypeRepository typeRepo,
            ITenantProvider tenantProvider,
            IUnitOfWork uow)
        {
            _service = service;
            _typeRepo = typeRepo;
            _tenantProvider = tenantProvider;
            _uow = uow;
        }

        [HttpGet("types")]
        public async Task<IActionResult> GetIntegrationTypes(CancellationToken ct)
        {
            var types = await _typeRepo.GetAllActiveAsync(ct);
            return Ok(types.Select(t => new { t.Id, t.Name, t.Source, t.DefaultBaseUrl, t.Description, t.IsActive }));
        }

        [HttpPost("types")]
        [Authorize(Policy = "SuperAdminOnly")]
        public async Task<IActionResult> AddIntegrationType([FromBody] AddIntegrationTypeDto dto, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(dto.Name) || string.IsNullOrWhiteSpace(dto.Source))
                return BadRequest("Name and Source are required.");

            var existing = await _typeRepo.GetBySourceAsync(dto.Source.Trim().ToLowerInvariant(), ct);
            if (existing != null)
                return BadRequest($"Integration type with source '{dto.Source}' already exists.");

            var entity = new Loyalty.DataAccess.Entities.Main.IntegrationType
            {
                Name = dto.Name.Trim(),
                Source = dto.Source.Trim().ToLowerInvariant(),
                DefaultBaseUrl = dto.DefaultBaseUrl?.Trim(),
                Description = dto.Description?.Trim(),
                IsActive = true
            };

            await _typeRepo.AddAndSaveAsync(entity, ct);
            return Ok(new { entity.Id, entity.Name, entity.Source, entity.DefaultBaseUrl, entity.Description });
        }

        [HttpPut("types/{id}")]
        [Authorize(Policy = "SuperAdminOnly")]
        public async Task<IActionResult> UpdateIntegrationType(int id, [FromBody] UpdateIntegrationTypeDto dto, CancellationToken ct)
        {
            var entity = await _typeRepo.GetByIdAsync(id, ct);
            if (entity == null)
                return NotFound("Integration type not found.");

            if (!string.IsNullOrWhiteSpace(dto.Name))
                entity.Name = dto.Name.Trim();
            if (!string.IsNullOrWhiteSpace(dto.Source))
                entity.Source = dto.Source.Trim().ToLowerInvariant();
            if (dto.DefaultBaseUrl != null)
                entity.DefaultBaseUrl = dto.DefaultBaseUrl.Trim();
            if (dto.Description != null)
                entity.Description = dto.Description.Trim();
            if (dto.IsActive.HasValue)
                entity.IsActive = dto.IsActive.Value;

            await _typeRepo.UpdateAndSaveAsync(entity, ct);
            return Ok(new { entity.Id, entity.Name, entity.Source, entity.DefaultBaseUrl, entity.Description, entity.IsActive });
        }

        [HttpPost]
        [Authorize(Policy = "TenantAdminOrModerator")]
        public async Task<IActionResult> Create([FromBody] TenantIntegrationCreateDto dto, CancellationToken ct)
        {
            var tenantId = dto.TenantId > 0 ? dto.TenantId : _tenantProvider.CurrentTenantId ?? 0;
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

        // ── Per-branch venues (iiko multi-org) ──

        [HttpGet("{id}/venues")]
        [Authorize(Policy = "TenantAdminOrModerator")]
        public async Task<IActionResult> ListVenues(int id, [FromQuery] int? tenantId, CancellationToken ct)
        {
            var tid = tenantId ?? _tenantProvider.CurrentTenantId;
            var result = await _service.ListVenuesAsync(id, tid, ct);
            if (!result.Success) return BadRequest(result.Message);
            return Ok(result.Data);
        }

        [HttpPost("{id}/venues")]
        [Authorize(Policy = "TenantAdminOrModerator")]
        public async Task<IActionResult> AddVenue(int id, [FromBody] TenantIntegrationVenueUpsertDto dto, [FromQuery] int? tenantId, CancellationToken ct)
        {
            var tid = tenantId ?? _tenantProvider.CurrentTenantId;
            var result = await _service.AddVenueAsync(id, dto, tid, ct);
            if (!result.Success) return BadRequest(result.Message);
            return Ok(new { id = result.Data });
        }

        [HttpPut("venues/{venueId}")]
        [Authorize(Policy = "TenantAdminOrModerator")]
        public async Task<IActionResult> UpdateVenue(int venueId, [FromBody] TenantIntegrationVenueUpsertDto dto, [FromQuery] int? tenantId, CancellationToken ct)
        {
            var tid = tenantId ?? _tenantProvider.CurrentTenantId;
            var result = await _service.UpdateVenueAsync(venueId, dto, tid, ct);
            if (!result.Success) return BadRequest(result.Message);
            return Ok();
        }

        [HttpDelete("venues/{venueId}")]
        [Authorize(Policy = "TenantAdminOrModerator")]
        public async Task<IActionResult> DeleteVenue(int venueId, [FromQuery] int? tenantId, CancellationToken ct)
        {
            var tid = tenantId ?? _tenantProvider.CurrentTenantId;
            var result = await _service.DeleteVenueAsync(venueId, tid, ct);
            if (!result.Success) return BadRequest(result.Message);
            return Ok();
        }

        /// <summary>Birdəfəlik düzəliş: venue filialı səhv olduğu dövrdə yazılmış iiko tx-lərinin BranchId-sini xəritə ilə dəyiş. DryRun default.</summary>
        [HttpPost("{id}/restamp-transaction-branches")]
        [Authorize(Policy = "SuperAdminOnly")]
        public async Task<IActionResult> RestampTransactionBranches(int id, [FromBody] RestampBranchesDto dto, CancellationToken ct)
        {
            var result = await _service.RestampIikoTransactionBranchesAsync(id, dto, ct);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        [HttpGet("{id}/iiko/organizations")]
        [Authorize(Policy = "TenantAdminOrModerator")]
        public async Task<IActionResult> GetIikoOrganizations(int id, [FromQuery] int? tenantId, CancellationToken ct)
        {
            var tid = tenantId ?? _tenantProvider.CurrentTenantId;
            var result = await _service.GetIikoOrganizationsAsync(id, tid, ct);
            if (!result.Success) return BadRequest(result.Message);
            return Ok(result.Data);
        }

        /// <summary>ExternalProgramId boş venue-ları iiko-dan avtomatik doldurur.</summary>
        [HttpPost("{id}/iiko/autofill-programs")]
        [Authorize(Policy = "TenantAdminOrModerator")]
        public async Task<IActionResult> AutoFillVenuePrograms(int id, [FromQuery] int? tenantId, CancellationToken ct)
        {
            var tid = tenantId ?? _tenantProvider.CurrentTenantId;
            var result = await _service.AutoFillVenueProgramsAsync(id, tid, ct);
            if (!result.Success) return BadRequest(result.Message);
            return Ok(new { filled = result.Data });
        }
    }
}

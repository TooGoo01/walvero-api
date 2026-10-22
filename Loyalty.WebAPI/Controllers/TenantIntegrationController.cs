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
        private readonly IIikoRulesService _iikoRules;
        private readonly IIikoCatalogService _iikoCatalog;
        private readonly IIikoConnectionResolver _iikoConnection;
        private readonly IPosEarnExclusionService _nomenclature;
        private readonly IIntegrationTypeRepository _typeRepo;
        private readonly ITenantProvider _tenantProvider;
        private readonly IUnitOfWork _uow;

        public TenantIntegrationController(
            ITenantIntegrationService service,
            IIikoRulesService iikoRules,
            IIikoCatalogService iikoCatalog,
            IIikoConnectionResolver iikoConnection,
            IPosEarnExclusionService nomenclature,
            IIntegrationTypeRepository typeRepo,
            ITenantProvider tenantProvider,
            IUnitOfWork uow)
        {
            _service = service;
            _iikoRules = iikoRules;
            _iikoCatalog = iikoCatalog;
            _iikoConnection = iikoConnection;
            _nomenclature = nomenclature;
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
            // Tenant-a bağlı işçi body-də başqa tenant göndərə bilməz
            var tenantId = this.ScopedTenantId(dto.TenantId > 0 ? dto.TenantId : null, _tenantProvider) ?? 0;
            if (tenantId <= 0) return BadRequest("TenantId required.");
            dto.TenantId = tenantId;
            if ((dto.SendPhoneToPos.HasValue || dto.MatchExistingByPhone.HasValue) && !User.IsInRole("Super Admin"))
            {
                dto.SendPhoneToPos = null;
                dto.MatchExistingByPhone = null;
            }

            var result = await _service.CreateAsync(dto, ct);
            if (!result.Success) return BadRequest(result.Message);
            return Ok(result.Data);
        }

        [HttpGet]
        [Authorize(Policy = "TenantAdminOrModerator")]
        public async Task<IActionResult> List([FromQuery] int? tenantId, CancellationToken ct)
        {
            var tid = this.ScopedTenantId(tenantId, _tenantProvider);
            if (!tid.HasValue || tid <= 0) return BadRequest("TenantId required.");
            var result = await _service.ListByTenantAsync(tid.Value, ct);
            return Ok(result.Data);
        }

        [HttpGet("{id}")]
        [Authorize(Policy = "TenantAdminOrModerator")]
        public async Task<IActionResult> GetById(int id, [FromQuery] int? tenantId, CancellationToken ct)
        {
            var tid = this.ScopedTenantId(tenantId, _tenantProvider);
            var result = await _service.GetByIdAsync(id, tid, ct);
            if (result.Data == null) return NotFound();
            return Ok(result.Data);
        }

        [HttpPut("{id}")]
        [Authorize(Policy = "TenantAdminOrModerator")]
        public async Task<IActionResult> Update(int id, [FromBody] TenantIntegrationUpdateDto dto, [FromQuery] int? tenantId, CancellationToken ct)
        {
            // Kart track rejimi (iiko-ya hansı identifikator gedir) yalnız super admin tərəfindən dəyişilir
            if ((dto.CardTrackMode.HasValue || dto.CardTrackDigits.HasValue) && !User.IsInRole("Super Admin"))
                return StatusCode(403, "Kart identifikator rejimini yalnız super admin dəyişə bilər.");
            // Telefonun kassaya göndərilməsi / telefonla köhnə müştəri uyğunlaşdırması — yalnız super admin
            if ((dto.SendPhoneToPos.HasValue || dto.MatchExistingByPhone.HasValue) && !User.IsInRole("Super Admin"))
                return StatusCode(403, "Telefon ayarlarını yalnız super admin dəyişə bilər.");
            var tid = this.ScopedTenantId(tenantId, _tenantProvider);
            var result = await _service.UpdateAsync(id, dto, tid, ct);
            if (!result.Success) return BadRequest(result.Message);
            return Ok();
        }

        [HttpDelete("{id}")]
        [Authorize(Policy = "TenantAdminOrModerator")]
        public async Task<IActionResult> Delete(int id, [FromQuery] int? tenantId, CancellationToken ct)
        {
            var tid = this.ScopedTenantId(tenantId, _tenantProvider);
            var result = await _service.DeleteAsync(id, tid, ct);
            if (!result.Success) return BadRequest(result.Message);
            return Ok();
        }

        [HttpPost("{id}/regenerate-wallet-token")]
        [Authorize(Policy = "TenantAdminOrModerator")]
        public async Task<IActionResult> RegenerateWalletToken(int id, [FromQuery] int? tenantId, CancellationToken ct)
        {
            var tid = this.ScopedTenantId(tenantId, _tenantProvider);
            var result = await _service.RegenerateWalletTokenAsync(id, tid, ct);
            if (!result.Success) return BadRequest(result.Message);
            return Ok(new { walletToken = result.Data });
        }

        // ── Per-branch venues (iiko multi-org) ──

        [HttpGet("{id}/venues")]
        [Authorize(Policy = "TenantAdminOrModerator")]
        public async Task<IActionResult> ListVenues(int id, [FromQuery] int? tenantId, CancellationToken ct)
        {
            var tid = this.ScopedTenantId(tenantId, _tenantProvider);
            var result = await _service.ListVenuesAsync(id, tid, ct);
            if (!result.Success) return BadRequest(result.Message);
            return Ok(result.Data);
        }

        [HttpPost("{id}/venues")]
        [Authorize(Policy = "TenantAdminOrModerator")]
        public async Task<IActionResult> AddVenue(int id, [FromBody] TenantIntegrationVenueUpsertDto dto, [FromQuery] int? tenantId, CancellationToken ct)
        {
            var tid = this.ScopedTenantId(tenantId, _tenantProvider);
            var result = await _service.AddVenueAsync(id, dto, tid, ct);
            if (!result.Success) return BadRequest(result.Message);
            return Ok(new { id = result.Data });
        }

        [HttpPut("venues/{venueId}")]
        [Authorize(Policy = "TenantAdminOrModerator")]
        public async Task<IActionResult> UpdateVenue(int venueId, [FromBody] TenantIntegrationVenueUpsertDto dto, [FromQuery] int? tenantId, CancellationToken ct)
        {
            var tid = this.ScopedTenantId(tenantId, _tenantProvider);
            var result = await _service.UpdateVenueAsync(venueId, dto, tid, ct);
            if (!result.Success) return BadRequest(result.Message);
            return Ok();
        }

        [HttpDelete("venues/{venueId}")]
        [Authorize(Policy = "TenantAdminOrModerator")]
        public async Task<IActionResult> DeleteVenue(int venueId, [FromQuery] int? tenantId, CancellationToken ct)
        {
            var tid = this.ScopedTenantId(tenantId, _tenantProvider);
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
            var tid = this.ScopedTenantId(tenantId, _tenantProvider);
            var result = await _service.GetIikoOrganizationsAsync(id, tid, ct);
            if (!result.Success) return BadRequest(result.Message);
            return Ok(result.Data);
        }

        /// <summary>
        /// iiko loyallıq proqramının qaydaları (kampaniyalar: hansı məhsula neçə %, şərtlər) — read-only, filial (venue) üzrə.
        /// Tenant admin öz tenant-ının inteqrasiyasını görür; super admin tenantId ilə.
        /// </summary>
        [HttpGet("{id}/iiko/rules")]
        [Authorize(Policy = "TenantAdminOrModerator")]
        public async Task<IActionResult> GetIikoRules(int id, [FromQuery] int? tenantId, [FromQuery] int? venueId, [FromQuery] bool refresh = false, CancellationToken ct = default)
        {
            var tid = this.ScopedTenantId(tenantId, _tenantProvider);
            if (!tid.HasValue || tid <= 0) return BadRequest("TenantId required.");
            var result = await _iikoRules.GetRulesAsync(tid.Value, id, venueId, refresh, ct);
            if (!result.Success) return BadRequest(result.Message);
            return Ok(result.Data);
        }

        /// <summary>
        /// iiko kataloqu: proqramlar + kampaniyalar, cüzdanlar, qonaq kateqoriyaları, məhsul qrupları/kateqoriyaları —
        /// seçim siyahıları, idxal və sinxron paneli üçün. Tenant admin öz inteqrasiyasını, super admin tenantId ilə.
        /// </summary>
        [HttpGet("{id}/iiko/catalog")]
        [Authorize(Policy = "TenantAdminOrModerator")]
        public async Task<IActionResult> GetIikoCatalog(int id, [FromQuery] int? tenantId, [FromQuery] bool refresh = false, CancellationToken ct = default)
        {
            var tid = this.ScopedTenantId(tenantId, _tenantProvider);
            if (!tid.HasValue || tid <= 0) return BadRequest("TenantId required.");
            var result = await _iikoCatalog.GetCatalogAsync(tid.Value, id, refresh, ct);
            if (!result.Success) return BadRequest(result.Message);
            return Ok(result.Data);
        }

        /// <summary>Kataloqda məhsul axtarışı (iiko nomenklaturası + sifarişlərdən yığılan məhsullar).</summary>
        [HttpGet("{id}/iiko/catalog/products")]
        [Authorize(Policy = "TenantAdminOrModerator")]
        public async Task<IActionResult> SearchIikoCatalogProducts(int id, [FromQuery] int? tenantId, [FromQuery] string? q,
            [FromQuery] string? groupId, [FromQuery] string? categoryId, [FromQuery] int take = 100, CancellationToken ct = default)
        {
            var tid = this.ScopedTenantId(tenantId, _tenantProvider);
            if (!tid.HasValue || tid <= 0) return BadRequest("TenantId required.");
            var (ti, error) = await _iikoConnection.GetOwnedIikoIntegrationAsync(tid.Value, id, ct);
            if (ti == null) return BadRequest(error);
            var result = await _nomenclature.SearchProductsAsync(id, q, groupId, categoryId, Math.Clamp(take, 1, 300), ct);
            if (!result.Success) return BadRequest(result.Message);
            return Ok(result.Data);
        }

        /// <summary>Nomenklaturanı iiko-dan yenilə (məhsul qrupları/kateqoriyaları).</summary>
        [HttpPost("{id}/iiko/catalog/refresh-nomenclature")]
        [Authorize(Policy = "TenantAdminOrModerator")]
        public async Task<IActionResult> RefreshIikoNomenclature(int id, [FromQuery] int? tenantId, CancellationToken ct)
        {
            var tid = this.ScopedTenantId(tenantId, _tenantProvider);
            if (!tid.HasValue || tid <= 0) return BadRequest("TenantId required.");
            var (ti, error) = await _iikoConnection.GetOwnedIikoIntegrationAsync(tid.Value, id, ct);
            if (ti == null) return BadRequest(error);
            var result = await _nomenclature.RefreshNomenclatureAsync(id, ct);
            if (!result.Success) return BadRequest(result.Message);
            return Ok(result.Data);
        }

        /// <summary>Kassa kampaniyalarından Walvero ayar təklifləri (önbaxış — heç nə yazılmır).</summary>
        [HttpGet("{id}/iiko/import/preview")]
        [Authorize(Policy = "TenantAdminOrModerator")]
        public async Task<IActionResult> PreviewIikoImport(int id, [FromQuery] int? tenantId, CancellationToken ct)
        {
            var tid = this.ScopedTenantId(tenantId, _tenantProvider);
            if (!tid.HasValue || tid <= 0) return BadRequest("TenantId required.");
            var result = await _iikoCatalog.PreviewImportAsync(tid.Value, id, ct);
            if (!result.Success) return BadRequest(result.Message);
            return Ok(result.Data);
        }

        public sealed record IikoImportApplyRequest(List<string> Keys);

        /// <summary>Seçilmiş idxal təkliflərini tətbiq et (proqram/şablon/inteqrasiya ayarları; lazım olsa ambassador proqramı yaradılır).</summary>
        [HttpPost("{id}/iiko/import/apply")]
        [Authorize(Policy = "TenantAdminOrSuperAdmin")]
        public async Task<IActionResult> ApplyIikoImport(int id, [FromBody] IikoImportApplyRequest request, [FromQuery] int? tenantId, CancellationToken ct)
        {
            var tid = this.ScopedTenantId(tenantId, _tenantProvider);
            if (!tid.HasValue || tid <= 0) return BadRequest("TenantId required.");
            var userId = int.TryParse(_tenantProvider.CurrentUserId, out var uid) ? uid : 1;
            var result = await _iikoCatalog.ApplyImportAsync(tid.Value, id, request?.Keys ?? new List<string>(), userId, ct);
            if (!result.Success) return BadRequest(result.Message);
            return Ok(result.Data);
        }

        /// <summary>Kassa ↔ Walvero sinxron vəziyyəti (bağlı/bağlanmamış cüzdanlar, riskli rejimlər, kateqoriya, kataloq).</summary>
        [HttpGet("{id}/iiko/sync-status")]
        [Authorize(Policy = "TenantAdminOrModerator")]
        public async Task<IActionResult> GetIikoSyncStatus(int id, [FromQuery] int? tenantId, CancellationToken ct)
        {
            var tid = this.ScopedTenantId(tenantId, _tenantProvider);
            if (!tid.HasValue || tid <= 0) return BadRequest("TenantId required.");
            var result = await _iikoCatalog.GetSyncStatusAsync(tid.Value, id, ct);
            if (!result.Success) return BadRequest(result.Message);
            return Ok(result.Data);
        }

        /// <summary>ExternalProgramId boş venue-ları iiko-dan avtomatik doldurur.</summary>
        [HttpPost("{id}/iiko/autofill-programs")]
        [Authorize(Policy = "TenantAdminOrModerator")]
        public async Task<IActionResult> AutoFillVenuePrograms(int id, [FromQuery] int? tenantId, CancellationToken ct)
        {
            var tid = this.ScopedTenantId(tenantId, _tenantProvider);
            var result = await _service.AutoFillVenueProgramsAsync(id, tid, ct);
            if (!result.Success) return BadRequest(result.Message);
            return Ok(new { filled = result.Data });
        }
    }
}

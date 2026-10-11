using Loyalty.Business.Helpers;
using Loyalty.Business.Results;
using Loyalty.Business.Services.Abstractions.Main;
using Loyalty.DataAccess.Enums;
using Loyalty.DataAccess.Context;
using Loyalty.DataAccess.Entities.Main;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Loyalty.DataAccess.Repositories.Abstractions.Main;
using Loyalty.DataAccess.UnitOfWorks;
using Loyalty.Infrastructure.Utilities.Security.Encryption;

namespace Loyalty.Business.Services.Implementations.Main
{
    public class TenantIntegrationService : ITenantIntegrationService
    {
        private readonly ITenantIntegrationRepository _repo;
        private readonly ITenantIntegrationVenueRepository _venueRepo;
        private readonly IIntegrationTypeRepository _typeRepo;
        private readonly IWalletTokenGenerator _tokenGenerator;
        private readonly ISensitiveDataProtector _protector;
        private readonly IIikoPosApiClient _iikoClient;
        private readonly IUnitOfWork _uow;
        private readonly LoyaltyWalletDbContext _db;
        private readonly ILogger<TenantIntegrationService> _logger;

        public async Task<ServiceResult<RestampBranchesResultDto>> RestampIikoTransactionBranchesAsync(int integrationId, RestampBranchesDto dto, CancellationToken ct = default)
        {
            if (dto?.Map == null || dto.Map.Count == 0)
                return ServiceResult<RestampBranchesResultDto>.Fail("Map boşdur.");
            if (dto.Map.Select(m => m.FromBranchId).Distinct().Count() != dto.Map.Count)
                return ServiceResult<RestampBranchesResultDto>.Fail("FromBranchId təkrarlanır.");

            var integration = await _repo.GetByIdAsync(integrationId, ct);
            if (integration == null)
                return ServiceResult<RestampBranchesResultDto>.Fail("İnteqrasiya tapılmadı.");

            var branchIds = dto.Map.SelectMany(m => new[] { m.FromBranchId, m.ToBranchId }).Distinct().ToList();
            var ownBranches = await _db.Set<Branch>()
                .Where(b => b.TenantId == integration.TenantId && branchIds.Contains(b.Id))
                .Select(b => b.Id).ToListAsync(ct);
            if (ownBranches.Count != branchIds.Count)
                return ServiceResult<RestampBranchesResultDto>.Fail("Filial bu tenant-a aid deyil.");

            var before = dto.Before;
            if (before == null)
            {
                before = await _db.Set<TenantIntegrationVenue>()
                    .Where(v => v.TenantIntegrationId == integrationId)
                    .Select(v => (DateTime?)(v.EditDate ?? v.RegDate))
                    .MinAsync(ct);
            }
            if (before == null)
                return ServiceResult<RestampBranchesResultDto>.Fail("Kəsim anı müəyyən edilmədi.");

            var map = dto.Map.ToDictionary(m => m.FromBranchId, m => m.ToBranchId);
            var fromIds = map.Keys.ToList();
            var cutoff = before.Value;

            // Yalnız iiko mənşəli tx-lər — kassir/scanner tx-ləri operator filialı ilə möhürlənib, toxunulmur
            var txs = await _db.Set<Transaction>()
                .Where(t => t.BranchId != null && fromIds.Contains(t.BranchId.Value)
                            && t.RegDate < cutoff
                            && t.LoyaltyCard.TenantId == integration.TenantId
                            && ((t.Description != null && t.Description.StartsWith("iiko"))
                                || (t.OrderId != null && t.OrderId.StartsWith("iiko:"))))
                .ToListAsync(ct);

            var result = new RestampBranchesResultDto
            {
                Before = cutoff,
                DryRun = dto.DryRun,
                Counts = dto.Map.ToDictionary(
                    m => $"{m.FromBranchId}->{m.ToBranchId}",
                    m => txs.Count(t => t.BranchId == m.FromBranchId))
            };
            if (dto.DryRun) return ServiceResult<RestampBranchesResultDto>.Ok(result);

            var now = DateTime.UtcNow;
            foreach (var t in txs)
            {
                t.BranchId = map[t.BranchId!.Value];
                t.EditDate = now;
            }
            await _db.SaveChangesAsync(ct);
            result.Updated = txs.Count;

            _logger.LogWarning(
                "iiko tx branch restamp: TenantId={TenantId}, IntegrationId={IntegrationId}, Before={Before}, Counts={Counts}, Updated={Updated}",
                integration.TenantId, integrationId, cutoff, string.Join(",", result.Counts.Select(kv => $"{kv.Key}:{kv.Value}")), result.Updated);

            return ServiceResult<RestampBranchesResultDto>.Ok(result);
        }

        public TenantIntegrationService(
            ITenantIntegrationRepository repo,
            ITenantIntegrationVenueRepository venueRepo,
            IIntegrationTypeRepository typeRepo,
            IWalletTokenGenerator tokenGenerator,
            ISensitiveDataProtector protector,
            IIikoPosApiClient iikoClient,
            IUnitOfWork uow,
            LoyaltyWalletDbContext db,
            ILogger<TenantIntegrationService> logger)
        {
            _db = db;
            _logger = logger;
            _repo = repo;
            _venueRepo = venueRepo;
            _typeRepo = typeRepo;
            _tokenGenerator = tokenGenerator;
            _protector = protector;
            _iikoClient = iikoClient;
            _uow = uow;
        }

        public async Task<ServiceResult<TenantIntegrationCreateResult>> CreateAsync(TenantIntegrationCreateDto dto, CancellationToken ct = default)
        {
            var existing = await _repo.GetActiveByTenantAndTypeAsync(dto.TenantId, dto.IntegrationTypeId, ct);
            if (existing != null)
                return ServiceResult<TenantIntegrationCreateResult>.Fail("Bu tenant üçün bu inteqrasiya artıq mövcuddur.");

            var (token, hash) = _tokenGenerator.Generate();
            var trackErr = ValidateCardTrack(dto.CardTrackMode, dto.CardTrackDigits);
            if (trackErr != null) return ServiceResult<TenantIntegrationCreateResult>.Fail(trackErr);
            var entity = new TenantIntegration
            {
                CardTrackMode = dto.CardTrackMode ?? PosCardTrackMode.FullBarcode,
                CardTrackDigits = (dto.CardTrackMode ?? PosCardTrackMode.FullBarcode) == PosCardTrackMode.LastDigits ? PosCardTrack.NormalizeDigits(dto.CardTrackDigits) : null,
                TenantId = dto.TenantId,
                IntegrationTypeId = dto.IntegrationTypeId,
                DisplayName = dto.DisplayName,
                ExternalVenueId = dto.ExternalVenueId,
                ExternalProgramId = dto.ExternalProgramId,
                ExternalTerminalGroupId = dto.ExternalTerminalGroupId,
                LoyaltyProgramId = dto.LoyaltyProgramId,
                ApiBaseUrl = dto.ApiBaseUrl?.Trim(),
                ApiUsername = dto.ApiUsername?.Trim(),
                ApiTokenEncrypted = !string.IsNullOrEmpty(dto.ApiToken) ? _protector.Encrypt(dto.ApiToken) : null,
                ApiAppId = dto.ApiAppId?.Trim(),
                ApiSecretEncrypted = !string.IsNullOrEmpty(dto.ApiSecret) ? _protector.Encrypt(dto.ApiSecret) : null,
                WalletTokenHash = hash,
                CreatedAt = DateTime.UtcNow,
                IsActive = true,
                RegUserId = 1,
                RegDate = DateTime.UtcNow
            };
            await _repo.AddAsync(entity);
            await _uow.CommitAsync(ct);

            var saved = await _repo.GetActiveByTenantAndTypeAsync(dto.TenantId, dto.IntegrationTypeId, ct);
            if (saved == null) return ServiceResult<TenantIntegrationCreateResult>.Fail("Yaradıla bilmədi.");

            return ServiceResult<TenantIntegrationCreateResult>.Ok(new TenantIntegrationCreateResult
            {
                Id = saved.Id,
                WalletToken = token,
                WalletWebhookBaseUrl = "api/wallet"
            });
        }

        public async Task<ServiceResult<List<TenantIntegrationListDto>>> ListByTenantAsync(int tenantId, CancellationToken ct = default)
        {
            var list = await _repo.GetAllAsync(ti => ti.TenantId == tenantId);
            var dtos = new List<TenantIntegrationListDto>();
            foreach (var ti in list)
            {
                var it = await _typeRepo.GetByIdAsync(ti.IntegrationTypeId, ct);
                dtos.Add(new TenantIntegrationListDto
                {
                    Id = ti.Id,
                    TenantId = ti.TenantId,
                    IntegrationTypeName = it?.Name ?? "",
                    Source = it?.Source ?? "",
                    DisplayName = ti.DisplayName,
                    ExternalVenueId = ti.ExternalVenueId,
                    IsActive = ti.IsActive,
                    CardTrackMode = ti.CardTrackMode,
                    CardTrackDigits = ti.CardTrackDigits,
                    CardTrackLabel = PosCardTrack.Describe(ti.CardTrackMode, ti.CardTrackDigits)
                });
            }
            return ServiceResult<List<TenantIntegrationListDto>>.Ok(dtos);
        }

        public async Task<ServiceResult<TenantIntegrationDetailDto?>> GetByIdAsync(int id, int? tenantId, CancellationToken ct = default)
        {
            var ti = await _repo.GetByIdAsync(id, ct);
            if (ti == null) return ServiceResult<TenantIntegrationDetailDto?>.Ok(null);
            if (tenantId.HasValue && ti.TenantId != tenantId.Value)
                return ServiceResult<TenantIntegrationDetailDto?>.Fail("Yetkisiz.");
            var it = await _typeRepo.GetByIdAsync(ti.IntegrationTypeId, ct);
            return ServiceResult<TenantIntegrationDetailDto?>.Ok(new TenantIntegrationDetailDto
            {
                Id = ti.Id,
                TenantId = ti.TenantId,
                IntegrationTypeId = ti.IntegrationTypeId,
                IntegrationTypeName = it?.Name ?? "",
                Source = it?.Source ?? "",
                DisplayName = ti.DisplayName,
                ExternalVenueId = ti.ExternalVenueId,
                ExternalProgramId = ti.ExternalProgramId,
                ExternalTerminalGroupId = ti.ExternalTerminalGroupId,
                LoyaltyProgramId = ti.LoyaltyProgramId,
                ApiBaseUrl = ti.ApiBaseUrl,
                ApiUsername = ti.ApiUsername,
                ApiAppId = ti.ApiAppId,
                IsActive = ti.IsActive,
                CardTrackMode = ti.CardTrackMode,
                CardTrackDigits = ti.CardTrackDigits,
                CardTrackLabel = PosCardTrack.Describe(ti.CardTrackMode, ti.CardTrackDigits)
            });
        }

        /// <summary>LastDigits üçün N 4–8 olmalıdır.</summary>
        private static string? ValidateCardTrack(PosCardTrackMode? mode, int? digits)
        {
            if (mode == PosCardTrackMode.LastDigits && digits.HasValue && (digits < PosCardTrack.MinDigits || digits > PosCardTrack.MaxDigits))
                return $"Son rəqəm sayı {PosCardTrack.MinDigits}–{PosCardTrack.MaxDigits} aralığında olmalıdır.";
            if (mode.HasValue && !Enum.IsDefined(typeof(PosCardTrackMode), mode.Value))
                return "Kart track rejimi düzgün deyil.";
            return null;
        }

        public async Task<ServiceResult<bool>> UpdateAsync(int id, TenantIntegrationUpdateDto dto, int? tenantId, CancellationToken ct = default)
        {
            var ti = await _repo.GetByIdAsync(id, ct);
            if (ti == null) return ServiceResult<bool>.Fail("Tapılmadı.");
            if (tenantId.HasValue && ti.TenantId != tenantId.Value)
                return ServiceResult<bool>.Fail("Yetkisiz.");

            if (dto.DisplayName != null) ti.DisplayName = dto.DisplayName;
            if (dto.ExternalVenueId != null) ti.ExternalVenueId = dto.ExternalVenueId;
            if (dto.ExternalProgramId != null) ti.ExternalProgramId = dto.ExternalProgramId;
            if (dto.ExternalTerminalGroupId != null) ti.ExternalTerminalGroupId = dto.ExternalTerminalGroupId;
            if (dto.LoyaltyProgramId.HasValue) ti.LoyaltyProgramId = dto.LoyaltyProgramId;
            if (dto.ApiBaseUrl != null) ti.ApiBaseUrl = dto.ApiBaseUrl.Trim();
            if (dto.ApiUsername != null) ti.ApiUsername = dto.ApiUsername.Trim();
            if (!string.IsNullOrEmpty(dto.ApiToken)) ti.ApiTokenEncrypted = _protector.Encrypt(dto.ApiToken);
            if (dto.ApiAppId != null) ti.ApiAppId = dto.ApiAppId.Trim();
            if (!string.IsNullOrEmpty(dto.ApiSecret)) ti.ApiSecretEncrypted = _protector.Encrypt(dto.ApiSecret);
            if (dto.IsActive.HasValue) ti.IsActive = dto.IsActive.Value;
            if (dto.CardTrackMode.HasValue || dto.CardTrackDigits.HasValue)
            {
                var mode = dto.CardTrackMode ?? ti.CardTrackMode;
                var trackErr = ValidateCardTrack(mode, dto.CardTrackDigits);
                if (trackErr != null) return ServiceResult<bool>.Fail(trackErr);
                ti.CardTrackMode = mode;
                ti.CardTrackDigits = mode == PosCardTrackMode.LastDigits
                    ? PosCardTrack.NormalizeDigits(dto.CardTrackDigits ?? ti.CardTrackDigits)
                    : null;
            }
            ti.UpdatedAt = DateTime.UtcNow;
            _repo.Update(ti);
            await _uow.CommitAsync(ct);
            return ServiceResult<bool>.Ok(true);
        }

        public async Task<ServiceResult<bool>> DeleteAsync(int id, int? tenantId, CancellationToken ct = default)
        {
            var ti = await _repo.GetByIdAsync(id, ct);
            if (ti == null) return ServiceResult<bool>.Fail("Tapılmadı.");
            if (tenantId.HasValue && ti.TenantId != tenantId.Value)
                return ServiceResult<bool>.Fail("Yetkisiz.");
            _repo.Delete(ti);
            await _uow.CommitAsync(ct);
            return ServiceResult<bool>.Ok(true);
        }

        public async Task<ServiceResult<string?>> RegenerateWalletTokenAsync(int id, int? tenantId, CancellationToken ct = default)
        {
            var ti = await _repo.GetByIdAsync(id, ct);
            if (ti == null) return ServiceResult<string?>.Fail("Tapılmadı.");
            if (tenantId.HasValue && ti.TenantId != tenantId.Value)
                return ServiceResult<string?>.Fail("Yetkisiz.");
            var (token, hash) = _tokenGenerator.Generate();
            ti.WalletTokenHash = hash;
            ti.UpdatedAt = DateTime.UtcNow;
            _repo.Update(ti);
            await _uow.CommitAsync(ct);
            return ServiceResult<string?>.Ok(token);
        }

        // ── Per-branch venues (iiko multi-org) ──

        private async Task<TenantIntegration?> GetOwnedIntegrationAsync(int integrationId, int? tenantId, CancellationToken ct)
        {
            var ti = await _repo.GetByIdAsync(integrationId, ct);
            if (ti == null) return null;
            if (tenantId.HasValue && ti.TenantId != tenantId.Value) return null;
            return ti;
        }

        public async Task<ServiceResult<List<TenantIntegrationVenueDto>>> ListVenuesAsync(int integrationId, int? tenantId, CancellationToken ct = default)
        {
            var ti = await GetOwnedIntegrationAsync(integrationId, tenantId, ct);
            if (ti == null) return ServiceResult<List<TenantIntegrationVenueDto>>.Fail("Tapılmadı.");

            var venues = await _venueRepo.GetActiveByIntegrationAsync(integrationId, ct);
            var list = venues.Select(v => new TenantIntegrationVenueDto
            {
                Id = v.Id,
                TenantIntegrationId = v.TenantIntegrationId,
                BranchId = v.BranchId,
                BranchName = v.Branch?.Name,
                ExternalVenueId = v.ExternalVenueId,
                ExternalTerminalGroupId = v.ExternalTerminalGroupId,
                ExternalProgramId = v.ExternalProgramId,
                DisplayName = v.DisplayName,
                IsActive = v.IsActive
            }).ToList();
            return ServiceResult<List<TenantIntegrationVenueDto>>.Ok(list);
        }

        public async Task<ServiceResult<int>> AddVenueAsync(int integrationId, TenantIntegrationVenueUpsertDto dto, int? tenantId, CancellationToken ct = default)
        {
            var ti = await GetOwnedIntegrationAsync(integrationId, tenantId, ct);
            if (ti == null) return ServiceResult<int>.Fail("Tapılmadı.");
            if (string.IsNullOrWhiteSpace(dto.ExternalVenueId))
                return ServiceResult<int>.Fail("ExternalVenueId (organizationId) tələb olunur.");

            var venue = new TenantIntegrationVenue
            {
                TenantIntegrationId = integrationId,
                BranchId = dto.BranchId,
                ExternalVenueId = dto.ExternalVenueId.Trim(),
                ExternalTerminalGroupId = string.IsNullOrWhiteSpace(dto.ExternalTerminalGroupId) ? null : dto.ExternalTerminalGroupId.Trim(),
                ExternalProgramId = string.IsNullOrWhiteSpace(dto.ExternalProgramId) ? null : dto.ExternalProgramId.Trim(),
                DisplayName = dto.DisplayName?.Trim(),
                IsActive = dto.IsActive ?? true,
                RegUserId = 1,
                RegDate = DateTime.UtcNow
            };
            await _venueRepo.AddAsync(venue);
            await _uow.CommitAsync(ct);
            return ServiceResult<int>.Ok(venue.Id);
        }

        public async Task<ServiceResult<bool>> UpdateVenueAsync(int venueId, TenantIntegrationVenueUpsertDto dto, int? tenantId, CancellationToken ct = default)
        {
            var venue = await _venueRepo.GetByIdAsync(venueId, ct);
            if (venue == null) return ServiceResult<bool>.Fail("Tapılmadı.");
            var ti = await GetOwnedIntegrationAsync(venue.TenantIntegrationId, tenantId, ct);
            if (ti == null) return ServiceResult<bool>.Fail("Yetkisiz.");

            if (dto.BranchId.HasValue) venue.BranchId = dto.BranchId;
            if (!string.IsNullOrWhiteSpace(dto.ExternalVenueId)) venue.ExternalVenueId = dto.ExternalVenueId.Trim();
            if (dto.ExternalTerminalGroupId != null) venue.ExternalTerminalGroupId = string.IsNullOrWhiteSpace(dto.ExternalTerminalGroupId) ? null : dto.ExternalTerminalGroupId.Trim();
            if (dto.ExternalProgramId != null) venue.ExternalProgramId = string.IsNullOrWhiteSpace(dto.ExternalProgramId) ? null : dto.ExternalProgramId.Trim();
            if (dto.DisplayName != null) venue.DisplayName = dto.DisplayName.Trim();
            if (dto.IsActive.HasValue) venue.IsActive = dto.IsActive.Value;
            venue.EditDate = DateTime.UtcNow;
            _venueRepo.Update(venue);
            await _uow.CommitAsync(ct);
            return ServiceResult<bool>.Ok(true);
        }

        public async Task<ServiceResult<bool>> DeleteVenueAsync(int venueId, int? tenantId, CancellationToken ct = default)
        {
            var venue = await _venueRepo.GetByIdAsync(venueId, ct);
            if (venue == null) return ServiceResult<bool>.Fail("Tapılmadı.");
            var ti = await GetOwnedIntegrationAsync(venue.TenantIntegrationId, tenantId, ct);
            if (ti == null) return ServiceResult<bool>.Fail("Yetkisiz.");
            _venueRepo.Delete(venue);
            await _uow.CommitAsync(ct);
            return ServiceResult<bool>.Ok(true);
        }

        /// <summary>
        /// ExternalProgramId boş olan venue-ları iiko-dan avtomatik doldurur:
        /// hər venue-nun org-u üçün loyalty proqramlarını çəkir, tək (və ya ilk)
        /// aktiv proqramı yazır. Feature-dən əvvəl yaradılmış venue-ları düzəldir.
        /// </summary>
        public async Task<ServiceResult<int>> AutoFillVenueProgramsAsync(int integrationId, int? tenantId, CancellationToken ct = default)
        {
            var ti = await GetOwnedIntegrationAsync(integrationId, tenantId, ct);
            if (ti == null) return ServiceResult<int>.Fail("Tapılmadı.");
            if (string.IsNullOrEmpty(ti.ApiTokenEncrypted))
                return ServiceResult<int>.Fail("apiLogin konfiqurasiya edilməyib.");

            var apiLogin = _protector.Decrypt(ti.ApiTokenEncrypted);
            if (string.IsNullOrEmpty(apiLogin))
                return ServiceResult<int>.Fail("apiLogin oxunmadı.");
            var clientSecret = string.IsNullOrEmpty(ti.ApiSecretEncrypted) ? null : _protector.Decrypt(ti.ApiSecretEncrypted);

            var token = await _iikoClient.GetAccessTokenAsync(apiLogin, ti.ApiAppId, clientSecret, ct: ct);
            if (string.IsNullOrEmpty(token))
                return ServiceResult<int>.Fail("iiko access_token alınmadı (credential-ları yoxla).");

            var venues = await _venueRepo.GetActiveByIntegrationAsync(integrationId, ct);
            int filled = 0;
            foreach (var v in venues)
            {
                if (!string.IsNullOrWhiteSpace(v.ExternalProgramId)) continue; // artıq dolu
                if (!Guid.TryParse(v.ExternalVenueId, out var orgId)) continue;   // org yoxdur

                List<IikoLoyaltyProgram>? programs = null;
                try { programs = await _iikoClient.GetLoyaltyProgramsAsync(token, orgId, ct); } catch { /* keç */ }
                var program = programs?.FirstOrDefault(p => p.IsActive) ?? programs?.FirstOrDefault();
                if (program == null) continue; // bu org-da proqram yoxdur

                v.ExternalProgramId = program.Id.ToString();
                v.EditDate = DateTime.UtcNow;
                _venueRepo.Update(v);
                filled++;
            }

            // Parent inteqrasiyada da ExternalProgramId boşdursa, ilk dolan venue-nunku ilə doldur (legacy fallback).
            if (filled > 0 && string.IsNullOrWhiteSpace(ti.ExternalProgramId))
            {
                var firstProgram = venues.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v.ExternalProgramId))?.ExternalProgramId;
                if (!string.IsNullOrWhiteSpace(firstProgram))
                {
                    ti.ExternalProgramId = firstProgram;
                    _repo.Update(ti);
                }
            }

            if (filled > 0) await _uow.CommitAsync(ct);
            return ServiceResult<int>.Ok(filled);
        }

        public async Task<ServiceResult<List<IikoOrgOptionDto>>> GetIikoOrganizationsAsync(int integrationId, int? tenantId, CancellationToken ct = default)
        {
            var ti = await GetOwnedIntegrationAsync(integrationId, tenantId, ct);
            if (ti == null) return ServiceResult<List<IikoOrgOptionDto>>.Fail("Tapılmadı.");
            if (string.IsNullOrEmpty(ti.ApiTokenEncrypted))
                return ServiceResult<List<IikoOrgOptionDto>>.Fail("apiLogin konfiqurasiya edilməyib.");

            var apiLogin = _protector.Decrypt(ti.ApiTokenEncrypted);
            if (string.IsNullOrEmpty(apiLogin))
                return ServiceResult<List<IikoOrgOptionDto>>.Fail("apiLogin oxunmadı.");
            var clientSecret = string.IsNullOrEmpty(ti.ApiSecretEncrypted) ? null : _protector.Decrypt(ti.ApiSecretEncrypted);

            var token = await _iikoClient.GetAccessTokenAsync(apiLogin, ti.ApiAppId, clientSecret, ct: ct);
            if (string.IsNullOrEmpty(token))
                return ServiceResult<List<IikoOrgOptionDto>>.Fail("iiko access_token alınmadı (credential-ları yoxla).");

            var orgs = await _iikoClient.GetOrganizationsAsync(token, ct);
            if (orgs == null || orgs.Length == 0)
                return ServiceResult<List<IikoOrgOptionDto>>.Ok(new List<IikoOrgOptionDto>());

            var tgBlocks = await _iikoClient.GetTerminalGroupsAsync(token, orgs.Select(o => o.Id), ct);
            var tgByOrg = (tgBlocks ?? new()).ToDictionary(b => b.OrganizationId, b => b.Items ?? new());

            var result = new List<IikoOrgOptionDto>();
            foreach (var o in orgs)
            {
                // Loyalty proqramları org üzrədir — ayrıca çağırış (xəta olsa boş keç).
                List<IikoLoyaltyProgram>? programs = null;
                try { programs = await _iikoClient.GetLoyaltyProgramsAsync(token, o.Id, ct); } catch { /* optional */ }

                result.Add(new IikoOrgOptionDto
                {
                    OrganizationId = o.Id.ToString(),
                    Name = o.Name,
                    TerminalGroups = (tgByOrg.TryGetValue(o.Id, out var items) ? items : new())
                        .Select(i => new IikoTerminalGroupOptionDto { Id = i.Id.ToString(), Name = i.Name })
                        .ToList(),
                    Programs = (programs ?? new())
                        .Select(p => new IikoProgramOptionDto { Id = p.Id.ToString(), Name = p.Name })
                        .ToList()
                });
            }

            return ServiceResult<List<IikoOrgOptionDto>>.Ok(result);
        }
    }
}

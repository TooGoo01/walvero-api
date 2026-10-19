using Loyalty.Business.Helpers;
using Loyalty.Business.Results;
using Loyalty.Business.Services.Abstractions.Main;
using Loyalty.DataAccess.Context;
using Loyalty.DataAccess.Entities.Main;
using Loyalty.DataAccess.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace Loyalty.Business.Services.Implementations.Main
{
    /// <summary>
    /// iiko kataloqu: proqramlar + kampaniyalar (IikoRulesService cache-i ilə), cüzdanlar (onlardan törənir),
    /// qonaq kateqoriyaları (ayrıca cache), nomenklatura (PosNomenclatureCache). Yalnız oxu.
    /// </summary>
    public sealed class IikoCatalogService : IIikoCatalogService
    {
        private static readonly TimeSpan GuestCategoryTtl = TimeSpan.FromMinutes(5);

        private readonly IIikoRulesService _rules;
        private readonly IIikoConnectionResolver _connection;
        private readonly IIikoPosApiClient _iiko;
        private readonly IPosEarnExclusionService _nomenclature;
        private readonly IMemoryCache _cache;
        private readonly LoyaltyWalletDbContext _db;
        private readonly ILogger<IikoCatalogService> _logger;

        public IikoCatalogService(
            IIikoRulesService rules,
            IIikoConnectionResolver connection,
            IIikoPosApiClient iiko,
            IPosEarnExclusionService nomenclature,
            IMemoryCache cache,
            LoyaltyWalletDbContext db,
            ILogger<IikoCatalogService> logger)
        {
            _db = db;
            _rules = rules;
            _connection = connection;
            _iiko = iiko;
            _nomenclature = nomenclature;
            _cache = cache;
            _logger = logger;
        }

        public async Task<ServiceResult<IikoCatalogDto>> GetCatalogAsync(int tenantId, int integrationId, bool refresh, CancellationToken ct = default)
        {
            var (ti, error) = await _connection.GetOwnedIikoIntegrationAsync(tenantId, integrationId, ct);
            if (ti == null) return ServiceResult<IikoCatalogDto>.Fail(error ?? "İnteqrasiya tapılmadı.");

            var result = new IikoCatalogDto
            {
                IntegrationId = ti.Id,
                DisplayName = ti.DisplayName ?? ti.IntegrationType?.Name,
                GeneratedAtUtc = DateTime.UtcNow
            };

            // 1) Proqramlar + kampaniyalar (5 dəq cache, IikoRulesService)
            var rules = await _rules.GetRulesAsync(tenantId, integrationId, null, refresh, ct);
            if (!rules.Success || rules.Data == null)
            {
                result.Errors.Add(rules.Message ?? "iiko proqramları alınmadı.");
            }
            else
            {
                result.Venues = rules.Data.Venues.Select(v => new IikoCatalogVenueDto
                {
                    VenueId = v.VenueId,
                    BranchName = v.BranchName,
                    DisplayName = v.DisplayName,
                    OrganizationId = v.OrganizationId,
                    Error = v.Error,
                    FetchedAtUtc = v.FetchedAtUtc,
                    FromCache = v.FromCache
                }).ToList();
                result.Programs = IikoCatalogMerge.Programs(rules.Data.Venues
                    .Where(v => v.Error == null)
                    .Select(v => (v.BranchName ?? v.DisplayName ?? v.OrganizationId, v.Programs)));
                result.Errors.AddRange(rules.Data.Venues.Where(v => v.Error != null)
                    .Select(v => $"{v.BranchName ?? v.DisplayName ?? v.OrganizationId}: {v.Error}"));
            }
            result.Wallets = IikoCatalogMerge.Wallets(result.Programs);

            // 2) Qonaq kateqoriyaları (org başına cache)
            var fetched = await FetchGuestCategoriesAsync(ti, refresh, result.Errors, ct);
            result.GuestCategories = IikoCatalogMerge.GuestCategories(fetched, result.Programs);

            // 3) Nomenklatura (DB cache — refresh ayrıca endpoint-lə)
            try
            {
                var nom = await _nomenclature.GetNomenclatureAsync(integrationId, ct);
                if (nom.Success) result.Nomenclature = nom.Data;
                else if (!string.IsNullOrWhiteSpace(nom.Message)) result.Errors.Add("Nomenklatura: " + nom.Message);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "iiko catalog: nomenclature failed. IntegrationId={Id}", integrationId);
                result.Errors.Add("Nomenklatura oxunmadı.");
            }

            return ServiceResult<IikoCatalogDto>.Ok(result);
        }

        // ───────────────────────── İdxal (önbaxış / tətbiq) + sinxron vəziyyəti ─────────────────────────

        public async Task<ServiceResult<IikoImportPreviewDto>> PreviewImportAsync(int tenantId, int integrationId, CancellationToken ct = default)
        {
            var catalog = await GetCatalogAsync(tenantId, integrationId, refresh: false, ct);
            if (!catalog.Success || catalog.Data == null)
                return ServiceResult<IikoImportPreviewDto>.Fail(catalog.Message ?? "Kassa kataloqu alınmadı.");
            var state = await LoadWalveroStateAsync(tenantId, integrationId, ct);
            return ServiceResult<IikoImportPreviewDto>.Ok(new IikoImportPreviewDto
            {
                Proposals = IikoImportPlanner.Plan(catalog.Data.Programs, state),
                Errors = catalog.Data.Errors
            });
        }

        public async Task<ServiceResult<IikoImportResultDto>> ApplyImportAsync(int tenantId, int integrationId, List<string> keys, int userId, CancellationToken ct = default)
        {
            if (keys == null || keys.Count == 0)
                return ServiceResult<IikoImportResultDto>.Fail("Heç bir təklif seçilməyib.");

            // Önbaxış serverdə yenidən hesablanır — UI-dakı köhnə vəziyyət yox, cari kassa + Walvero vəziyyəti tətbiq olunur
            var preview = await PreviewImportAsync(tenantId, integrationId, ct);
            if (!preview.Success || preview.Data == null)
                return ServiceResult<IikoImportResultDto>.Fail(preview.Message ?? "Önbaxış alınmadı.");

            var result = new IikoImportResultDto();
            var selected = new List<IikoImportProposalDto>();
            foreach (var key in keys.Distinct())
            {
                var p = preview.Data.Proposals.FirstOrDefault(x => x.Key == key);
                if (p != null && p.Applicable) selected.Add(p);
                else result.Skipped.Add(key);
            }
            if (selected.Count == 0)
                return ServiceResult<IikoImportResultDto>.Ok(result);
            if (selected.Where(p => p.Group != null).GroupBy(p => p.Group).Any(g => g.Count() > 1))
                return ServiceResult<IikoImportResultDto>.Fail("Eyni kampaniya üçün yalnız bir variant seçilə bilər (kassa və ya Walvero hesablasın).");

            // EnableRetryOnFailure açıqdır → əl ilə transaction execution strategy içində olmalıdır
            var now = DateTime.UtcNow;
            LoyaltyProgram? created = null;
            var strategy = _db.Database.CreateExecutionStrategy();
            await strategy.ExecuteAsync(async () =>
            {
            _db.ChangeTracker.Clear(); // təkrar cəhddə əvvəlki cəhdin izləri qalmasın
            created = null;
            result.Applied.Clear();
            await using var tx = await _db.Database.BeginTransactionAsync(ct);
            var integration = await _db.TenantIntegrations.FirstAsync(t => t.Id == integrationId && t.TenantId == tenantId, ct);

            foreach (var proposal in selected)
            {
                foreach (var change in proposal.Changes)
                {
                    switch (change.Target)
                    {
                        case IikoImportPlanner.TargetProgram:
                        {
                            var prog = await _db.LoyaltyPrograms.FirstAsync(x => x.Id == change.ProgramId && x.TenantId == tenantId, ct);
                            ApplyProgramField(prog, change.Field, change.To);
                            prog.EditDate = now;
                            prog.EditUserId = userId;
                            break;
                        }
                        case IikoImportPlanner.TargetTemplate:
                        {
                            var prog = await _db.LoyaltyPrograms.FirstAsync(x => x.Id == change.ProgramId && x.TenantId == tenantId, ct);
                            if (prog.CardTemplateId == null) break;
                            var tpl = await _db.CardTemplates.FirstAsync(x => x.Id == prog.CardTemplateId, ct);
                            if (change.Field == "BronzePercent" && int.TryParse(change.To, out var pct)) tpl.BronzePercent = pct;
                            if (change.Field == "Currency" && !string.IsNullOrWhiteSpace(change.To)) tpl.Currency = change.To!;
                            tpl.EditDate = now;
                            tpl.EditUserId = userId;
                            break;
                        }
                        case IikoImportPlanner.TargetNewAmbassadorProgram:
                        {
                            created ??= await CreateAmbassadorProgramAsync(tenantId, userId, now, ct);
                            if (change.Field is "PosWalletId" or "PosExternalProgramId")
                                ApplyProgramField(created, change.Field, change.To);
                            break;
                        }
                        case IikoImportPlanner.TargetIntegration:
                        {
                            if (change.Field == "AmbassadorIikoCategoryId")
                                integration.AmbassadorIikoCategoryId = string.IsNullOrWhiteSpace(change.To) ? null : change.To;
                            integration.UpdatedAt = now;
                            break;
                        }
                    }
                }
                result.Applied.Add(proposal.Key);
            }

            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            });
            result.CreatedProgramId = created?.Id;
            _logger.LogInformation("Kassa idxalı tətbiq olundu. TenantId={TenantId}, IntegrationId={IntegrationId}, UserId={UserId}, Applied=[{Applied}], CreatedProgram={Created}",
                tenantId, integrationId, userId, string.Join(",", result.Applied), created?.Id);
            return ServiceResult<IikoImportResultDto>.Ok(result);
        }

        public async Task<ServiceResult<IikoSyncStatusDto>> GetSyncStatusAsync(int tenantId, int integrationId, CancellationToken ct = default)
        {
            var catalog = await GetCatalogAsync(tenantId, integrationId, refresh: false, ct);
            if (!catalog.Success || catalog.Data == null)
                return ServiceResult<IikoSyncStatusDto>.Fail(catalog.Message ?? "Kassa kataloqu alınmadı.");
            var state = await LoadWalveroStateAsync(tenantId, integrationId, ct);
            var items = IikoImportPlanner.Status(catalog.Data, state);
            var level = items.Any(i => i.Level == "error") ? "error" : items.Any(i => i.Level == "warn") ? "warn" : "ok";
            return ServiceResult<IikoSyncStatusDto>.Ok(new IikoSyncStatusDto { GeneratedAtUtc = DateTime.UtcNow, Level = level, Items = items });
        }

        private static void ApplyProgramField(LoyaltyProgram prog, string field, string? value)
        {
            switch (field)
            {
                case "PosEarnSource":
                    if (Enum.TryParse<PosEarnSource>(value, out var src)) prog.PosEarnSource = src;
                    break;
                case "PosWalletId":
                    prog.PosWalletId = string.IsNullOrWhiteSpace(value) ? null : value;
                    break;
                case "PosExternalProgramId":
                    prog.PosExternalProgramId = string.IsNullOrWhiteSpace(value) ? null : value;
                    break;
            }
        }

        /// <summary>Ambassador proqramı + xal kartı şablonu (valyuta «məhsul»); dizayn adi proqramın şablonundan kopyalanır.</summary>
        private async Task<LoyaltyProgram> CreateAmbassadorProgramAsync(int tenantId, int userId, DateTime now, CancellationToken ct)
        {
            var code = "AMBASSADOR";
            for (var i = 1; await _db.LoyaltyPrograms.AnyAsync(p => p.TenantId == tenantId && p.Code == code, ct); i++)
                code = $"AMBASSADOR-{i}";

            var program = new LoyaltyProgram
            {
                TenantId = tenantId,
                ProgramName = "Ambassador",
                Code = code,
                ProgramType = LoyaltyProgramType.PointsBased,
                Audience = ProgramAudience.Ambassador,
                PosEarnSource = PosEarnSource.PosMirror,
                PointsPerCurrency = 1,
                CurrencyPerPoint = 1,
                IsActive = true,
                RegUserId = userId,
                RegDate = now
            };
            _db.LoyaltyPrograms.Add(program);
            await _db.SaveChangesAsync(ct);

            var source = await _db.LoyaltyPrograms.AsNoTracking()
                .Where(p => p.TenantId == tenantId && p.IsActive && p.Audience != ProgramAudience.Ambassador && p.CardTemplate != null)
                .OrderBy(p => p.Id)
                .Select(p => p.CardTemplate!)
                .FirstOrDefaultAsync(ct);

            var template = new CardTemplate
            {
                LoyaltyProgramId = program.Id,
                TemplateName = "Ambassador",
                TemplateType = CardTemplateType.PointsBased,
                Currency = IikoImportPlanner.UnitCurrency,
                BackgroundColor = source?.BackgroundColor ?? "#FFFFFF",
                ForegroundColor = source?.ForegroundColor ?? "#000000",
                LabelColor = source?.LabelColor ?? "#999999",
                ProgressBarColor = source?.ProgressBarColor ?? "#4CAF50",
                LogoUrl = source?.LogoUrl,
                GoogleLogoUrl = source?.GoogleLogoUrl,
                AppLogoUrl = source?.AppLogoUrl,
                StripImageUrl = source?.StripImageUrl,
                HeroImageUrl = source?.HeroImageUrl,
                AppStripUrl = source?.AppStripUrl,
                RegUserId = userId,
                RegDate = now
            };
            _db.CardTemplates.Add(template);
            await _db.SaveChangesAsync(ct);

            program.CardTemplateId = template.Id;
            return program;
        }

        private async Task<WalveroPosState> LoadWalveroStateAsync(int tenantId, int integrationId, CancellationToken ct)
        {
            var integration = await _db.TenantIntegrations.AsNoTracking()
                .Where(t => t.Id == integrationId && t.TenantId == tenantId)
                .Select(t => new { t.LoyaltyProgramId, t.AmbassadorIikoCategoryId })
                .FirstOrDefaultAsync(ct);
            var programs = await _db.LoyaltyPrograms.AsNoTracking()
                .Where(p => p.TenantId == tenantId && p.IsActive)
                .OrderBy(p => p.Id)
                .Select(p => new
                {
                    p.Id, p.ProgramName, p.Audience, p.PosEarnSource, p.PosWalletId, p.PosExternalProgramId,
                    TemplateType = p.CardTemplate != null ? (CardTemplateType?)p.CardTemplate.TemplateType : null,
                    RewardAsPoints = p.CardTemplate != null && p.CardTemplate.RewardAsPoints,
                    BronzePercent = p.CardTemplate != null ? p.CardTemplate.BronzePercent : null,
                    Currency = p.CardTemplate != null ? p.CardTemplate.Currency : null
                })
                .ToListAsync(ct);

            return new WalveroPosState
            {
                TenantId = tenantId,
                AmbassadorsEnabled = await _db.Tenants.AsNoTracking().Where(t => t.Id == tenantId).Select(t => t.EnableAmbassadors).FirstOrDefaultAsync(ct),
                IntegrationProgramId = integration?.LoyaltyProgramId,
                AmbassadorCategoryId = integration?.AmbassadorIikoCategoryId,
                Programs = programs.Select(p => new WalveroProgramState
                {
                    Id = p.Id,
                    Name = p.ProgramName,
                    Audience = p.Audience,
                    // ProgressBased + RewardAsPoints = xal kartı kimi davranır (CardTemplate.EffectiveType)
                    TemplateType = p.TemplateType == CardTemplateType.ProgressBased && p.RewardAsPoints ? CardTemplateType.PointsBased : p.TemplateType,
                    PosEarnSource = p.PosEarnSource,
                    PosWalletId = p.PosWalletId,
                    PosExternalProgramId = p.PosExternalProgramId,
                    BronzePercent = p.BronzePercent,
                    Currency = p.Currency
                }).ToList(),
                EarnRuleCount = await _db.PosProductEarnRules.CountAsync(r => r.TenantIntegrationId == integrationId && r.IsActive, ct),
                ExclusionCount = await _db.PosEarnExclusions.CountAsync(e => e.TenantIntegrationId == integrationId && e.IsActive, ct)
            };
        }

        private async Task<List<IikoGuestCategory>> FetchGuestCategoriesAsync(
            DataAccess.Entities.Main.TenantIntegration ti, bool refresh, List<string> errors, CancellationToken ct)
        {
            var all = new List<IikoGuestCategory>();
            var orgs = await _connection.GetOrgsAsync(ti, ct);
            string? token = null;
            var tokenTried = false;

            foreach (var org in orgs)
            {
                if (!Guid.TryParse(org.OrganizationId, out var orgId)) continue;
                var key = $"iiko_gcat_{ti.Id}_{orgId:N}";
                if (!refresh && _cache.TryGetValue(key, out List<IikoGuestCategory>? cached) && cached != null)
                {
                    all.AddRange(cached);
                    continue;
                }

                if (!tokenTried)
                {
                    tokenTried = true;
                    string? tokenError;
                    (token, tokenError) = await _connection.GetTokenAsync(ti, ct);
                    if (token == null && tokenError != null) errors.Add("Qonaq kateqoriyaları: " + tokenError);
                }
                if (token == null) break;

                try
                {
                    var list = await _iiko.GetGuestCategoriesAsync(token, orgId, ct);
                    if (list == null)
                    {
                        errors.Add($"Qonaq kateqoriyaları alınmadı ({org.BranchName ?? org.DisplayName ?? org.OrganizationId}).");
                        continue;
                    }
                    _cache.Set(key, list, new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = GuestCategoryTtl });
                    all.AddRange(list);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "iiko guest categories failed. IntegrationId={Id}, Org={Org}", ti.Id, orgId);
                    errors.Add($"Qonaq kateqoriyaları alınmadı ({org.BranchName ?? org.DisplayName ?? org.OrganizationId}).");
                }
            }
            return all;
        }
    }
}

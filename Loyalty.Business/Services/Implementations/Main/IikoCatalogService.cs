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
        private static readonly TimeSpan DiscountTypeTtl = TimeSpan.FromMinutes(10);
        private static readonly TimeSpan NomenclatureAutoRefreshBackoff = TimeSpan.FromMinutes(10);

        private readonly IIikoRulesService _rules;
        private readonly IIikoConnectionResolver _connection;
        private readonly IIikoPosApiClient _iiko;
        private readonly IPosEarnExclusionService _nomenclature;
        private readonly IMemoryCache _cache;
        private readonly LoyaltyWalletDbContext _db;
        private readonly IAmbassadorProgramFactory _ambassadorFactory;
        private readonly ILogger<IikoCatalogService> _logger;

        public IikoCatalogService(
            IIikoRulesService rules,
            IIikoConnectionResolver connection,
            IIikoPosApiClient iiko,
            IPosEarnExclusionService nomenclature,
            IMemoryCache cache,
            LoyaltyWalletDbContext db,
            IAmbassadorProgramFactory ambassadorFactory,
            ILogger<IikoCatalogService> logger)
        {
            _db = db;
            _ambassadorFactory = ambassadorFactory;
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

            // 3) Nomenklatura (DB cache). Surət heç çəkilməyibsə (və ya refresh) — iiko-dan çək ki, seçim pəncərələri boş qalmasın.
            //    Uğursuz cəhd 10 dəq təkrarlanmır (apiLogin yoxdur / lisenziya bitib).
            try
            {
                var hasSnapshot = await _db.PosNomenclatureCaches.AsNoTracking()
                    .AnyAsync(c => c.TenantIntegrationId == integrationId, ct);
                var tryKey = $"iiko_nom_autorefresh_{integrationId}";
                if (refresh || (!hasSnapshot && !_cache.TryGetValue(tryKey, out _)))
                {
                    _cache.Set(tryKey, true, NomenclatureAutoRefreshBackoff);
                    var r = await _nomenclature.RefreshNomenclatureAsync(integrationId, ct);
                    if (!r.Success && !string.IsNullOrWhiteSpace(r.Message))
                        result.Errors.Add("Nomenklatura yenilənmədi: " + r.Message);
                }

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
            var state = await LoadWalveroStateAsync(tenantId, integrationId, ct, catalog.Data.Nomenclature);
            return ServiceResult<IikoImportPreviewDto>.Ok(new IikoImportPreviewDto
            {
                Proposals = IikoImportPlanner.Plan(catalog.Data.Programs, state),
                Errors = catalog.Data.Errors,
                DiscountOptions = state.ObservedDiscounts
            });
        }

        public async Task<ServiceResult<IikoImportResultDto>> ApplyImportAsync(int tenantId, int integrationId, List<string> keys,
            IReadOnlyDictionary<string, string>? discountTypes, int userId, CancellationToken ct = default)
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

            // Hədiyyə endirimi: admin seçimi təklifi əvəz edir; yalnız sifarişlərdə görünmüş endirimlər qəbul olunur
            var discountOptions = preview.Data.DiscountOptions.Select(d => d.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var chosenDiscount = new Dictionary<string, string?>();
            foreach (var p in selected.Where(p => p.Kind == IikoImportPlanner.KindGiftReward))
            {
                string? chosen = p.DiscountTypeId;
                if (discountTypes != null && discountTypes.TryGetValue(p.Key, out var picked) && !string.IsNullOrWhiteSpace(picked))
                {
                    if (!discountOptions.Contains(picked.Trim()))
                        return ServiceResult<IikoImportResultDto>.Fail("Seçilmiş kassa endirimi sifarişlərdə görünən endirimlər arasında yoxdur.");
                    chosen = picked.Trim();
                }
                if (chosen == null)
                    return ServiceResult<IikoImportResultDto>.Fail($"«{p.Title}»: kassa endirimini seçin — onsuz hədiyyə sifarişdə tanınmır.");
                chosenDiscount[p.Key] = chosen;
            }
            var productNames = await LoadObservedProductNamesAsync(integrationId,
                selected.SelectMany(p => p.Changes)
                    .Where(c => c.Target == IikoImportPlanner.TargetReward && c.Field == IikoImportPlanner.FieldProducts)
                    .SelectMany(c => (c.To ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)),
                ct);

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
                            // Kassa idxalı: say balansı («məhsul»), kassa hesablayır
                            created ??= await _ambassadorFactory.CreateAsync(tenantId, userId, AmbassadorCardKind.Units, null, null,
                                PosEarnSource.PosMirror, now, ct);
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
                        case IikoImportPlanner.TargetEarnRule:
                        {
                            var prog = await _db.LoyaltyPrograms.Include(x => x.CardTemplate)
                                .FirstAsync(x => x.Id == change.ProgramId && x.TenantId == tenantId, ct);
                            var units = (int)Math.Max(1, Math.Round(change.Amount ?? 1m));
                            // Möhür kartında addım, xal kartında (RewardAsPoints daxil) xal
                            var stamps = prog.CardTemplate?.EffectiveType == CardTemplateType.ProgressBased;
                            var isCategory = change.Field == IikoImportPlanner.FieldCategory;
                            _db.PosProductEarnRules.Add(new PosProductEarnRule
                            {
                                TenantIntegrationId = integrationId,
                                LoyaltyProgramId = prog.Id,
                                Kind = isCategory ? PosEarnExclusionKind.Category : PosEarnExclusionKind.Product,
                                ExternalProductId = change.To!,
                                Code = isCategory ? null : change.To,
                                ProductName = change.TargetName,
                                StepsPerUnit = stamps ? units : 0,
                                PointsPerUnit = stamps ? 0 : units,
                                IsActive = true,
                                RegUserId = userId,
                                RegDate = now
                            });
                            break;
                        }
                    }
                }

                var rewardChanges = proposal.Changes.Where(c => c.Target == IikoImportPlanner.TargetReward).ToList();
                if (rewardChanges.Count > 0)
                    await ApplyRewardChangesAsync(tenantId, rewardChanges, chosenDiscount.GetValueOrDefault(proposal.Key), productNames, userId, now, ct);
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

        /// <summary>
        /// Bir hədiyyə təklifinin reward dəyişiklikləri: RewardId varsa mövcudu yeniləyir, yoxdursa yaradır.
        /// Products = əlavə olunacaq kassa kodları; endirim admin seçimi (və ya təklif) ilə.
        /// </summary>
        private async Task ApplyRewardChangesAsync(int tenantId, List<IikoImportChangeDto> changes, string? discountTypeId,
            IReadOnlyDictionary<string, string> productNames, int userId, DateTime now, CancellationToken ct)
        {
            string? Value(string field) => changes.FirstOrDefault(c => c.Field == field)?.To;
            var programId = changes[0].ProgramId!.Value;
            var program = await _db.LoyaltyPrograms.FirstAsync(x => x.Id == programId && x.TenantId == tenantId, ct);

            Reward reward;
            if (int.TryParse(Value(IikoImportPlanner.FieldRewardId), out var rewardId))
            {
                reward = await _db.Rewards.Include(r => r.PosProducts)
                    .FirstAsync(r => r.Id == rewardId && r.LoyaltyProgramId == program.Id, ct);
                reward.EditDate = now;
                reward.EditUserId = userId;
            }
            else
            {
                reward = new Reward
                {
                    LoyaltyProgramId = program.Id,
                    Name = (Value(IikoImportPlanner.FieldName) ?? "Hədiyyə").Trim(),
                    PosSourceKey = Value(IikoImportPlanner.FieldSourceKey),
                    IsActive = true,
                    RegUserId = userId,
                    RegDate = now
                };
                _db.Rewards.Add(reward);
            }

            if (int.TryParse(Value(IikoImportPlanner.FieldPointsCost), out var cost)) reward.PointsCost = cost;
            if (!string.IsNullOrWhiteSpace(discountTypeId)) reward.PosDiscountTypeId = discountTypeId;

            var codes = (Value(IikoImportPlanner.FieldProducts) ?? "")
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            foreach (var code in codes)
            {
                if (reward.PosProducts.Any(p => string.Equals(p.Code, code, StringComparison.OrdinalIgnoreCase))) continue;
                reward.PosProducts.Add(new RewardPosProduct
                {
                    Code = code,
                    ProductName = productNames.GetValueOrDefault(code),
                    IsActive = true,
                    RegUserId = userId,
                    RegDate = now
                });
            }
        }

        /// <summary>Kassa kodu → məhsul adı (sifarişlərdə görünən məhsullardan).</summary>
        private async Task<IReadOnlyDictionary<string, string>> LoadObservedProductNamesAsync(int integrationId, IEnumerable<string> codes, CancellationToken ct)
        {
            var list = codes.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            if (list.Count == 0) return new Dictionary<string, string>();
            var rows = await _db.PosObservedProducts.AsNoTracking()
                .Where(p => p.TenantIntegrationId == integrationId && p.ProductNumber != null && list.Contains(p.ProductNumber) && p.Name != null)
                .Select(p => new { p.ProductNumber, p.Name })
                .ToListAsync(ct);
            return rows.GroupBy(r => r.ProductNumber!, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First().Name!, StringComparer.OrdinalIgnoreCase);
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
        private async Task<WalveroPosState> LoadWalveroStateAsync(int tenantId, int integrationId, CancellationToken ct,
            PosNomenclatureDto? nomenclature = null)
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
                    Currency = p.CardTemplate != null ? p.CardTemplate.Currency : null,
                    TotalSteps = p.CardTemplate != null ? (int?)p.CardTemplate.TotalSteps : null
                })
                .ToListAsync(ct);
            var programIds = programs.Select(p => p.Id).ToList();

            var earnRules = await _db.PosProductEarnRules.AsNoTracking()
                .Where(r => r.TenantIntegrationId == integrationId && r.IsActive)
                .Select(r => new WalveroEarnRuleState(r.LoyaltyProgramId, r.Kind, r.ExternalProductId, r.Code))
                .ToListAsync(ct);
            var rewards = await _db.Rewards.AsNoTracking()
                .Where(r => programIds.Contains(r.LoyaltyProgramId) && r.PosSourceKey != null)
                .Select(r => new WalveroRewardState
                {
                    Id = r.Id, ProgramId = r.LoyaltyProgramId, Name = r.Name, PointsCost = r.PointsCost,
                    PosSourceKey = r.PosSourceKey, PosDiscountTypeId = r.PosDiscountTypeId,
                    ProductCodes = r.PosProducts.Select(p => p.Code).ToList()
                })
                .ToListAsync(ct);
            var observedDiscounts = await _db.PosObservedDiscounts.AsNoTracking()
                .Where(x => x.TenantIntegrationId == integrationId && x.IsActive)
                .OrderByDescending(x => x.LastSeenAtUtc)
                .Select(x => new IikoDiscountOptionDto(x.DiscountTypeId, x.Name, x.LastSeenAtUtc))
                .ToListAsync(ct);

            // Kateqoriya adı → ID: nomenklatura surəti + sifarişlərdə görünən məhsulların kateqoriyaları
            var categoryIdByName = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var c in nomenclature?.Categories ?? new())
                if (!string.IsNullOrWhiteSpace(c.Name)) categoryIdByName.TryAdd(c.Name.Trim(), c.Id);
            var observedCategories = await _db.PosObservedProducts.AsNoTracking()
                .Where(p => p.TenantIntegrationId == integrationId && p.CategoryId != null && p.CategoryName != null)
                .Select(p => new { p.CategoryId, p.CategoryName })
                .Distinct()
                .ToListAsync(ct);
            foreach (var c in observedCategories)
                categoryIdByName.TryAdd(c.CategoryName!.Trim(), c.CategoryId!);

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
                    Currency = p.Currency,
                    TotalSteps = p.TotalSteps
                }).ToList(),
                EarnRuleCount = earnRules.Count,
                ExclusionCount = await _db.PosEarnExclusions.CountAsync(e => e.TenantIntegrationId == integrationId && e.IsActive, ct),
                EarnRules = earnRules,
                Rewards = rewards,
                ObservedDiscounts = observedDiscounts,
                CategoryIdByName = categoryIdByName
            };
        }

        public async Task<ServiceResult<IikoDiscountCandidatesDto>> GetDiscountCandidatesAsync(
            int tenantId, int integrationId, bool refresh, CancellationToken ct = default)
        {
            var (ti, error) = await _connection.GetOwnedIikoIntegrationAsync(tenantId, integrationId, ct);
            if (ti == null) return ServiceResult<IikoDiscountCandidatesDto>.Fail(error ?? "İnteqrasiya tapılmadı.");

            var result = new IikoDiscountCandidatesDto();
            var catalog = await FetchDiscountTypesAsync(ti, refresh, result.Errors, ct);
            var observed = await _db.PosObservedDiscounts.AsNoTracking()
                .Where(x => x.TenantIntegrationId == integrationId && x.IsActive)
                .Select(x => new ObservedDiscountSource(x.DiscountTypeId, x.Name, x.LastSeenAtUtc))
                .ToListAsync(ct);
            var linked = await _db.PosLoyaltyDiscountTypes.AsNoTracking()
                .Where(x => x.TenantIntegrationId == integrationId && x.IsActive)
                .Select(x => x.ExternalDiscountTypeId)
                .ToListAsync(ct);

            result.Items = DiscountCandidateMerge.Merge(
                catalog.Select(d => new DiscountCandidateSource(d.Id.ToString(), d.Name, d.Percent, d.IsDeleted, d.IsManual)),
                observed,
                linked);
            return ServiceResult<IikoDiscountCandidatesDto>.Ok(result);
        }

        /// <summary>Org-ların endirim növləri (org başına cache). iiko cavab verməsə xəta siyahıya düşür, sifarişlərdən görünənlər qalır.</summary>
        private async Task<List<IikoDiscountType>> FetchDiscountTypesAsync(
            DataAccess.Entities.Main.TenantIntegration ti, bool refresh, List<string> errors, CancellationToken ct)
        {
            var all = new List<IikoDiscountType>();
            var orgs = await _connection.GetOrgsAsync(ti, ct);
            string? token = null;
            var tokenTried = false;

            foreach (var org in orgs)
            {
                if (!Guid.TryParse(org.OrganizationId, out var orgId)) continue;
                var key = $"iiko_disc_{ti.Id}_{orgId:N}";
                if (!refresh && _cache.TryGetValue(key, out List<IikoDiscountType>? cached) && cached != null)
                {
                    all.AddRange(cached);
                    continue;
                }

                if (!tokenTried)
                {
                    tokenTried = true;
                    string? tokenError;
                    (token, tokenError) = await _connection.GetTokenAsync(ti, ct);
                    if (token == null && tokenError != null) errors.Add("Kassa endirimləri: " + tokenError);
                }
                if (token == null) break;

                try
                {
                    var list = await _iiko.GetDiscountTypesAsync(token, orgId, ct);
                    if (list == null)
                    {
                        errors.Add($"Kassa endirimləri alınmadı ({org.BranchName ?? org.DisplayName ?? org.OrganizationId}).");
                        continue;
                    }
                    _cache.Set(key, list, new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = DiscountTypeTtl });
                    all.AddRange(list);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "iiko discounts failed. IntegrationId={Id}, Org={Org}", ti.Id, orgId);
                    errors.Add($"Kassa endirimləri alınmadı ({org.BranchName ?? org.DisplayName ?? org.OrganizationId}).");
                }
            }
            return all;
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

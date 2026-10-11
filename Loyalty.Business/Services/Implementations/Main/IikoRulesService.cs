using Loyalty.Business.Helpers;
using Loyalty.Business.Results;
using Loyalty.Business.Services.Abstractions.Main;
using Loyalty.DataAccess.Context;
using Loyalty.Infrastructure.Utilities.Security.Encryption;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace Loyalty.Business.Services.Implementations.Main
{
    public sealed class IikoRulesService : IIikoRulesService
    {
        private const string IikoSource = "iiko_pos";
        private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(5);

        private readonly LoyaltyWalletDbContext _db;
        private readonly IIikoPosApiClient _iiko;
        private readonly ISensitiveDataProtector _protector;
        private readonly IPosEarnExclusionService _exclusions;
        private readonly IMemoryCache _cache;
        private readonly ILogger<IikoRulesService> _logger;

        public IikoRulesService(
            LoyaltyWalletDbContext db,
            IIikoPosApiClient iiko,
            ISensitiveDataProtector protector,
            IPosEarnExclusionService exclusions,
            IMemoryCache cache,
            ILogger<IikoRulesService> logger)
        {
            _db = db;
            _iiko = iiko;
            _protector = protector;
            _exclusions = exclusions;
            _cache = cache;
            _logger = logger;
        }

        /// <summary>Bir org üçün cache-lənən hissə (token/credential cache-də saxlanmır).</summary>
        private sealed class OrgRules
        {
            public DateTime FetchedAtUtc;
            public string? Error;
            public List<IikoProgramSummary> Programs = new();
            public List<IikoRulesNamedDto> Categories = new();
            public int ProductCount;
        }

        public async Task<ServiceResult<IikoRulesResultDto>> GetRulesAsync(int tenantId, int integrationId, int? venueId, bool refresh, CancellationToken ct = default)
        {
            var ti = await _db.TenantIntegrations.AsNoTracking()
                .Include(t => t.IntegrationType)
                .FirstOrDefaultAsync(t => t.Id == integrationId && t.TenantId == tenantId, ct);
            if (ti == null) return ServiceResult<IikoRulesResultDto>.Fail("İnteqrasiya tapılmadı.");
            if (ti.IntegrationType?.Source != IikoSource)
                return ServiceResult<IikoRulesResultDto>.Fail("Qaydalar yalnız iiko inteqrasiyası üçün göstərilir.");

            var result = new IikoRulesResultDto
            {
                IntegrationId = ti.Id,
                DisplayName = ti.DisplayName ?? ti.IntegrationType?.Name,
                CardTrackMode = ti.CardTrackMode,
                CardTrackDigits = ti.CardTrackDigits,
                CardTrackLabel = PosCardTrack.Describe(ti.CardTrackMode, ti.CardTrackDigits),
                GeneratedAtUtc = DateTime.UtcNow
            };

            // Org-lar: aktiv venue-lar (filial adı ilə); yoxdursa legacy ExternalVenueId
            var venues = await _db.TenantIntegrationVenues.AsNoTracking()
                .Where(v => v.TenantIntegrationId == integrationId && v.IsActive)
                .Select(v => new { v.Id, v.BranchId, BranchName = v.Branch != null ? v.Branch.Name : null, v.DisplayName, v.ExternalVenueId })
                .OrderBy(v => v.Id)
                .ToListAsync(ct);
            if (venueId.HasValue) venues = venues.Where(v => v.Id == venueId.Value).ToList();

            var targets = venues
                .Select(v => new IikoRulesVenueDto { VenueId = v.Id, BranchId = v.BranchId, BranchName = v.BranchName, DisplayName = v.DisplayName, OrganizationId = v.ExternalVenueId })
                .ToList();
            if (targets.Count == 0 && !venueId.HasValue && !string.IsNullOrWhiteSpace(ti.ExternalVenueId))
                targets.Add(new IikoRulesVenueDto { OrganizationId = ti.ExternalVenueId, DisplayName = ti.DisplayName });
            if (targets.Count == 0)
                return ServiceResult<IikoRulesResultDto>.Fail(venueId.HasValue ? "Filial (venue) tapılmadı." : "iiko təşkilatı (venue) konfiqurasiya edilməyib.");

            // Token lazım olduqda bir dəfə (cache-dən gələnlər üçün alınmır)
            string? token = null;
            var tokenTried = false;
            string? tokenError = null;

            foreach (var target in targets)
            {
                if (!Guid.TryParse(target.OrganizationId, out var orgId))
                {
                    target.Error = "organizationId GUID deyil.";
                    target.FetchedAtUtc = DateTime.UtcNow;
                    continue;
                }

                var key = $"iiko_rules_{integrationId}_{orgId:N}";
                if (!refresh && _cache.TryGetValue(key, out OrgRules? cached) && cached != null)
                {
                    Apply(target, cached, fromCache: true);
                    continue;
                }

                if (!tokenTried)
                {
                    tokenTried = true;
                    (token, tokenError) = await GetTokenAsync(ti, ct);
                }
                if (string.IsNullOrEmpty(token))
                {
                    target.Error = tokenError ?? "iiko access_token alınmadı.";
                    target.FetchedAtUtc = DateTime.UtcNow;
                    continue;
                }

                var fresh = await FetchOrgAsync(token, orgId, integrationId, ct);
                if (fresh.Error == null)
                    _cache.Set(key, fresh, new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = CacheTtl });
                Apply(target, fresh, fromCache: false);
            }

            result.Venues = targets;

            // Walvero tərəfi (müqayisə üçün)
            try
            {
                var ex = await _exclusions.ListAsync(integrationId, ct);
                if (ex.Success && ex.Data != null) result.Walvero.Exclusions = ex.Data.Where(e => e.IsActive).ToList();
            }
            catch (Exception e)
            {
                _logger.LogWarning(e, "iiko rules: exclusions list failed. IntegrationId={Id}", integrationId);
            }
            result.Walvero.EarnRulesCount = await _db.PosProductEarnRules
                .CountAsync(r => r.TenantIntegrationId == integrationId && r.IsActive, ct);

            return ServiceResult<IikoRulesResultDto>.Ok(result);
        }

        private static void Apply(IikoRulesVenueDto target, OrgRules src, bool fromCache)
        {
            target.Error = src.Error;
            target.FetchedAtUtc = src.FetchedAtUtc;
            target.FromCache = fromCache;
            target.Programs = src.Programs;
            target.Categories = src.Categories;
            target.ProductCount = src.ProductCount;
            target.CashbackPercent = src.Programs
                .Where(p => p.IsActive)
                .Select(IikoRuleParser.ProgramCashbackPercent)
                .FirstOrDefault(p => p.HasValue);
        }

        private async Task<(string? Token, string? Error)> GetTokenAsync(DataAccess.Entities.Main.TenantIntegration ti, CancellationToken ct)
        {
            if (string.IsNullOrEmpty(ti.ApiTokenEncrypted)) return (null, "apiLogin konfiqurasiya edilməyib.");
            var apiLogin = _protector.Decrypt(ti.ApiTokenEncrypted);
            if (string.IsNullOrEmpty(apiLogin)) return (null, "apiLogin oxunmadı.");
            var clientSecret = string.IsNullOrEmpty(ti.ApiSecretEncrypted) ? null : _protector.Decrypt(ti.ApiSecretEncrypted);
            try
            {
                var token = await _iiko.GetAccessTokenAsync(apiLogin, ti.ApiAppId, clientSecret, ct: ct);
                return string.IsNullOrEmpty(token) ? (null, "iiko access_token alınmadı (credential-ları yoxla).") : (token, null);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "iiko rules: token failed. IntegrationId={Id}", ti.Id);
                return (null, "iiko access_token alınmadı.");
            }
        }

        private async Task<OrgRules> FetchOrgAsync(string token, Guid orgId, int integrationId, CancellationToken ct)
        {
            var r = new OrgRules { FetchedAtUtc = DateTime.UtcNow };

            // Ad həlli: nomenklatura (kateqoriya / qrup / məhsul id + kod) + sifarişlərdə görünmüş məhsullar
            var names = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                var nom = await _iiko.GetNomenclatureAsync(token, orgId, ct);
                if (nom != null)
                {
                    foreach (var c in nom.ProductCategories ?? new())
                    {
                        if (c.IsDeleted) continue;
                        r.Categories.Add(new IikoRulesNamedDto { Id = c.Id.ToString(), Name = c.Name });
                        if (!string.IsNullOrEmpty(c.Name)) names.TryAdd(c.Id.ToString(), c.Name);
                    }
                    foreach (var g in nom.Groups ?? new())
                        if (!g.IsDeleted && !string.IsNullOrEmpty(g.Name)) names.TryAdd(g.Id.ToString(), g.Name);
                    foreach (var p in nom.Products ?? new())
                    {
                        if (p.IsDeleted) continue;
                        r.ProductCount++;
                        if (string.IsNullOrEmpty(p.Name)) continue;
                        names.TryAdd(p.Id.ToString(), p.Name);
                        if (!string.IsNullOrWhiteSpace(p.Code)) names.TryAdd(p.Code.Trim(), p.Name);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "iiko rules: nomenclature failed. OrgId={OrgId}", orgId);
            }

            try
            {
                var observed = await _db.PosObservedProducts.AsNoTracking()
                    .Where(x => x.TenantIntegrationId == integrationId)
                    .Select(x => new { x.ProductId, x.ProductNumber, x.Name, x.CategoryId, x.CategoryName })
                    .ToListAsync(ct);
                foreach (var o in observed)
                {
                    if (!string.IsNullOrEmpty(o.Name))
                    {
                        if (!string.IsNullOrEmpty(o.ProductId)) names.TryAdd(o.ProductId, o.Name);
                        if (!string.IsNullOrEmpty(o.ProductNumber)) names.TryAdd(o.ProductNumber, o.Name);
                    }
                    if (!string.IsNullOrEmpty(o.CategoryId) && !string.IsNullOrEmpty(o.CategoryName))
                        names.TryAdd(o.CategoryId, o.CategoryName);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "iiko rules: observed products failed. IntegrationId={Id}", integrationId);
            }

            List<IikoLoyaltyProgramDetail>? programs = null;
            try { programs = await _iiko.GetLoyaltyProgramsDetailedAsync(token, orgId, ct); }
            catch (Exception ex) { _logger.LogWarning(ex, "iiko rules: programs failed. OrgId={OrgId}", orgId); }

            if (programs == null)
            {
                r.Error = "iiko proqram siyahısı alınmadı (org iikoCard-a bağlı deyil və ya cavab yoxdur).";
                return r;
            }

            string? Resolve(string id) => names.TryGetValue(id, out var n) ? n : null;
            r.Programs = programs.Select(p => IikoRuleParser.Summarize(p, Resolve)).ToList();
            return r;
        }
    }
}

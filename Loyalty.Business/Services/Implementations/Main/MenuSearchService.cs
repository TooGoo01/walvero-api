using Loyalty.Business.Dtos.Chatbot;
using Loyalty.Business.Services.Abstractions.Main;
using Loyalty.DataAccess.Context;
using Loyalty.DataAccess.Entities.Main;
using Loyalty.DataAccess.Repositories.Abstractions.Main;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace Loyalty.Business.Services.Implementations.Main
{
    /// <summary>
    /// Yeməyə/məhsula görə məkan axtarışı — chatbot `search_menu_items` tool-u.
    /// Yalnız "discoverable" (aktiv, app-da görünən) tenant-ların menyusuna baxır;
    /// internetə çıxmır. Nəticə PlaceResultDto (Flutter kartı) + yemək adı/qiyməti.
    /// </summary>
    public class MenuSearchService : IMenuSearchService
    {
        private readonly LoyaltyWalletDbContext _db;
        private readonly IBranchRepository _branchRepository;
        private readonly IMemoryCache _cache;

        // PlaceSearchService ilə eyni açar — discoverable branch siyahısı paylaşılır.
        private const string BranchCacheKey = "assistant:discoverable-branches";
        private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(60);

        public MenuSearchService(
            LoyaltyWalletDbContext db,
            IBranchRepository branchRepository,
            IMemoryCache cache)
        {
            _db = db;
            _branchRepository = branchRepository;
            _cache = cache;
        }

        public async Task<List<MenuItemResultDto>> SearchAsync(MenuItemSearchQuery query, CancellationToken ct = default)
        {
            var rawTokens = (query.Keywords ?? Array.Empty<string>())
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .Select(t => t.Trim())
                .ToList();
            if (rawTokens.Count == 0)
                return new List<MenuItemResultDto>();

            var normTokens = rawTokens.Select(Normalize).Where(t => t.Length > 0).Distinct().ToList();
            if (normTokens.Count == 0)
                return new List<MenuItemResultDto>();

            // Discoverable filiallar (cache paylaşılır) → tenant → nümayəndə filial + branchId lookup.
            var branches = await GetBranchesAsync(ct);
            var discoverableTenantIds = branches.Select(b => b.TenantId).Distinct().ToHashSet();
            var branchById = branches.ToDictionary(b => b.Id, b => b);
            var repBranchByTenant = branches
                .GroupBy(b => b.TenantId)
                .ToDictionary(g => g.Key, g => g.OrderByDescending(b => b.IsMainBranch).First());

            if (discoverableTenantIds.Count == 0)
                return new List<MenuItemResultDto>();

            // DB-səviyyə daralt: ən uzun (ən spesifik) token üzrə LIKE, yalnız aktiv + discoverable tenant.
            var primary = rawTokens.OrderByDescending(t => t.Length).First();
            var like = $"%{primary}%";

            var candidates = await _db.MenuItems
                .AsNoTracking()
                .Include(m => m.MenuCategory)
                .Where(m => m.IsActive && m.MenuCategory.IsActive)
                .Where(m => discoverableTenantIds.Contains(m.TenantId ?? m.MenuCategory.TenantId))
                .Where(m =>
                    EF.Functions.Like(m.AzName, like) ||
                    EF.Functions.Like(m.EnName, like) ||
                    EF.Functions.Like(m.RuName, like) ||
                    (m.AzDescription != null && EF.Functions.Like(m.AzDescription, like)) ||
                    (m.EnDescription != null && EF.Functions.Like(m.EnDescription, like)) ||
                    (m.RuDescription != null && EF.Functions.Like(m.RuDescription, like)))
                .Take(300)
                .ToListAsync(ct);

            bool hasGeo = query.Lat.HasValue && query.Lng.HasValue;
            bool geoFilter = hasGeo && query.RadiusKm.HasValue;

            // Yaddaşda tam skorla (bütün tokenlər, normalizasiya ilə) + filiala bağla.
            var scored = new List<(MenuItem item, Branch branch, int score, double? distanceKm)>();

            foreach (var m in candidates)
            {
                var haystack = Normalize(string.Join(" ", new[]
                {
                    m.AzName, m.EnName, m.RuName, m.AzDescription, m.EnDescription, m.RuDescription
                }.Where(s => !string.IsNullOrWhiteSpace(s))));

                int score = normTokens.Count(tok => haystack.Contains(tok, StringComparison.Ordinal));
                if (score == 0) continue;

                var effBranchId = m.BranchId ?? m.MenuCategory.BranchId;
                var effTenantId = m.TenantId ?? m.MenuCategory.TenantId;

                Branch? branch = null;
                if (effBranchId.HasValue && branchById.TryGetValue(effBranchId.Value, out var b))
                    branch = b;
                else if (repBranchByTenant.TryGetValue(effTenantId, out var rep))
                    branch = rep;

                if (branch == null) continue; // discoverable filial tapılmadı

                double? distanceKm = null;
                if (hasGeo && branch.Latitude.HasValue && branch.Longitude.HasValue)
                    distanceKm = Haversine(query.Lat!.Value, query.Lng!.Value,
                        (double)branch.Latitude.Value, (double)branch.Longitude.Value);

                if (geoFilter && (distanceKm is null || distanceKm > query.RadiusKm!.Value))
                    continue;

                scored.Add((m, branch, score, distanceKm));
            }

            // Hər filial üçün ən yüksək skorlu yeməyi saxla.
            var perBranch = scored
                .GroupBy(x => x.branch.Id)
                .Select(g => g.OrderByDescending(x => x.score).ThenBy(x => x.item.Price).First());

            IEnumerable<(MenuItem item, Branch branch, int score, double? distanceKm)> ordered = geoFilter
                ? perBranch.OrderBy(x => x.distanceKm ?? double.MaxValue).ThenByDescending(x => x.score)
                : perBranch.OrderByDescending(x => x.score).ThenBy(x => x.distanceKm ?? double.MaxValue);

            int take = Math.Clamp(query.Take <= 0 ? 8 : query.Take, 1, 20);

            return ordered.Take(take).Select(x => new MenuItemResultDto
            {
                Place = MapPlace(x.branch, x.distanceKm),
                DishName = FirstNonEmpty(x.item.AzName, x.item.EnName, x.item.RuName),
                Description = FirstNonEmptyOrNull(x.item.AzDescription, x.item.EnDescription, x.item.RuDescription),
                Price = x.item.DiscountPrice > 0 ? x.item.DiscountPrice : x.item.Price,
                CurrencySign = x.item.CurrencySign,
            }).ToList();
        }

        private async Task<List<Branch>> GetBranchesAsync(CancellationToken ct)
        {
            if (_cache.TryGetValue(BranchCacheKey, out List<Branch>? cached) && cached is not null)
                return cached;
            var list = await _branchRepository.GetDiscoverableAsync(ct);
            _cache.Set(BranchCacheKey, list, CacheTtl);
            return list;
        }

        private static PlaceResultDto MapPlace(Branch b, double? distanceKm)
        {
            var t = b.Tenant!;
            var name = !string.IsNullOrWhiteSpace(b.AppDisplayName)
                ? b.AppDisplayName!
                : (b.IsMainBranch ? t.CompanyName : $"{t.CompanyName} — {b.Name}");

            bool inApp = t.IsHasMenu && !string.IsNullOrWhiteSpace(t.Subdomain);

            return new PlaceResultDto
            {
                TenantId = t.Id,
                BranchId = b.Id,
                Name = name,
                CategoryName = t.Category?.AzName ?? t.Category?.EnName,
                Address = b.Address,
                DistanceKm = distanceKm.HasValue ? Math.Round(distanceKm.Value, 1) : (double?)null,
                Phone = b.Phone,
                LogoUrl = b.LogoUrl ?? t.Branding?.LogoUrl,
                LocationUrl = b.LocationUrl ?? t.LocationUrl,
                Menu = new PlaceMenuDto
                {
                    InApp = inApp,
                    TenantSlug = t.Subdomain,
                    BranchSlug = b.Slug,
                    ExternalMenuUrl = string.IsNullOrWhiteSpace(b.MenuUrl) ? t.MenuUrl : b.MenuUrl
                }
            };
        }

        private static string FirstNonEmpty(params string?[] vals)
            => vals.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v)) ?? string.Empty;

        private static string? FirstNonEmptyOrNull(params string?[] vals)
            => vals.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));

        private static string Normalize(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return string.Empty;
            s = s.ToLowerInvariant().Trim();
            var map = new Dictionary<char, char>
            {
                ['ə'] = 'e', ['ğ'] = 'g', ['ı'] = 'i', ['ö'] = 'o', ['ü'] = 'u',
                ['ş'] = 's', ['ç'] = 'c'
            };
            var chars = s.Select(c => map.TryGetValue(c, out var r) ? r : c).ToArray();
            return new string(chars);
        }

        private static double Haversine(double lat1, double lng1, double lat2, double lng2)
        {
            double latRad = lat1 * Math.PI / 180.0;
            double lngRad = lng1 * Math.PI / 180.0;
            double bLatRad = lat2 * Math.PI / 180.0;
            double bLngRad = lng2 * Math.PI / 180.0;
            double cos = Math.Cos(latRad) * Math.Cos(bLatRad) * Math.Cos(bLngRad - lngRad)
                       + Math.Sin(latRad) * Math.Sin(bLatRad);
            cos = Math.Max(-1.0, Math.Min(1.0, cos));
            return 6371.0 * Math.Acos(cos);
        }
    }
}

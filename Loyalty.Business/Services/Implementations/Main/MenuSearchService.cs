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

        // Normalizə edilmiş menyu indeksi — DB LIKE diakritik problemini aradan
        // qaldırır (model "albali" göndərəndə DB-dəki "Albalı" tapılmırdı).
        private const string MenuIndexCacheKey = "assistant:menu-index";
        private static readonly TimeSpan MenuIndexTtl = TimeSpan.FromSeconds(120);

        /// <summary>Axtarış üçün əvvəlcədən normalizə olunmuş menyu sətri.</summary>
        private sealed class MenuIndexEntry
        {
            public int EffTenantId;
            public int? SpecificBranchId;   // sətir filial-spesifikdirsə həmin filial
            public string DishName = string.Empty;
            public string? Description;
            public double Price;
            public double DiscountPrice;
            public string? CurrencySign;
            public string CategoryName = string.Empty;
            public string CategoryNorm = string.Empty;
            public string NameKey = string.Empty;   // normalizə edilmiş əsas ad (qruplaşdırma)
            public string NameHay = string.Empty;   // bütün dillərdə adlar (normalizə)
            public string FullHay = string.Empty;   // adlar + təsvirlər (normalizə)
        }

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

            var index = await GetMenuIndexAsync(ct);

            // Skorlama: ad hitə üstünlük (yanlış yemək seçimini azaldır).
            var joinedNorm = Normalize(string.Join(" ", rawTokens));
            var matched = new List<(MenuIndexEntry entry, int score)>();

            foreach (var e in index)
            {
                if (!discoverableTenantIds.Contains(e.EffTenantId)) continue;

                int score = 0;
                bool allInName = true;
                foreach (var tok in normTokens)
                {
                    if (e.NameHay.Contains(tok, StringComparison.Ordinal)) score += 3;
                    else
                    {
                        allInName = false;
                        if (e.FullHay.Contains(tok, StringComparison.Ordinal)) score += 1;
                    }
                }
                if (score == 0) continue;
                if (allInName) score += 5;
                if (e.NameKey.Length > 0 &&
                    (e.NameKey == joinedNorm || normTokens.Contains(e.NameKey)))
                    score += 10;

                matched.Add((e, score));
            }

            bool hasGeo = query.Lat.HasValue && query.Lng.HasValue;
            bool geoFilter = hasGeo && query.RadiusKm.HasValue;

            // Filiala bağla. Per-branch qayda: eyni yemək (tenant + NameKey) üçün
            // filial-spesifik sətir varsa, tenant-wide sətir HƏMİN filialı örtmür —
            // qiymət filialın öz sətrindən gəlir.
            var specificBranchKeys = matched
                .Where(x => x.entry.SpecificBranchId.HasValue)
                .Select(x => (x.entry.EffTenantId, x.entry.NameKey, BranchId: x.entry.SpecificBranchId!.Value))
                .ToHashSet();

            var scored = new List<(MenuIndexEntry entry, Branch branch, int score, bool specific, double? distanceKm)>();

            foreach (var (e, score) in matched)
            {
                Branch? branch = null;
                bool specific = false;

                if (e.SpecificBranchId.HasValue)
                {
                    if (!branchById.TryGetValue(e.SpecificBranchId.Value, out var b))
                        continue; // filial discoverable deyil
                    branch = b;
                    specific = true;
                }
                else
                {
                    if (!repBranchByTenant.TryGetValue(e.EffTenantId, out var rep))
                        continue;
                    // Nümayəndə filialın bu yemək üçün öz sətri varsa, tenant-wide sətri atla.
                    if (specificBranchKeys.Contains((e.EffTenantId, e.NameKey, rep.Id)))
                        continue;
                    branch = rep;
                }

                double? distanceKm = null;
                if (hasGeo && branch.Latitude.HasValue && branch.Longitude.HasValue)
                    distanceKm = Haversine(query.Lat!.Value, query.Lng!.Value,
                        (double)branch.Latitude.Value, (double)branch.Longitude.Value);

                if (geoFilter && (distanceKm is null || distanceKm > query.RadiusKm!.Value))
                    continue;

                scored.Add((e, branch, score, specific, distanceKm));
            }

            // Məkan filtri: "X-də Y neçəyədir?" — filial/tenant adına görə daralt.
            var placeNorm = Normalize(query.PlaceName ?? string.Empty);
            bool placeScoped = placeNorm.Length > 0;
            if (placeScoped)
            {
                var placeFiltered = scored.Where(x => BranchMatchesPlace(x.branch, placeNorm)).ToList();
                if (placeFiltered.Count > 0) scored = placeFiltered;
                else placeScoped = false; // uyğun məkan tapılmadı → normal "kim satır" davranışı
            }

            IEnumerable<(MenuIndexEntry entry, Branch branch, int score, bool specific, double? distanceKm)> candidates;
            if (placeScoped)
            {
                // Məkan verilib ("Irish Pub-da hansı sushi var") → filial-başına-1 dedup ATMA,
                // həmin məkanın BÜTÜN uyğun yeməklərini qaytar.
                candidates = scored;
            }
            else
            {
                // "Kim X satır" — hər filial üçün ən yaxşı sətir (skor, sonra filial-spesifik).
                candidates = scored
                    .GroupBy(x => x.branch.Id)
                    .Select(g => g
                        .OrderByDescending(x => x.score)
                        .ThenByDescending(x => x.specific)
                        .First());
            }

            IEnumerable<(MenuIndexEntry entry, Branch branch, int score, bool specific, double? distanceKm)> ordered = geoFilter
                ? candidates.OrderBy(x => x.distanceKm ?? double.MaxValue).ThenByDescending(x => x.score)
                : candidates.OrderByDescending(x => x.score).ThenBy(x => x.distanceKm ?? double.MaxValue);

            // Məkan-daxili siyahıda daha çox item lazımdır (kateqoriya-tipli sual) —
            // dispatch Take=8 göndərsə də məkan halında 20-yə qaldır.
            int take = placeScoped ? 20 : Math.Clamp(query.Take <= 0 ? 8 : query.Take, 1, 20);

            return ordered.Take(take).Select(x => new MenuItemResultDto
            {
                Place = MapPlace(x.branch, x.distanceKm),
                DishName = x.entry.DishName,
                Description = x.entry.Description,
                Price = x.entry.DiscountPrice > 0 ? x.entry.DiscountPrice : x.entry.Price,
                RegularPrice = x.entry.DiscountPrice > 0 ? x.entry.Price : (double?)null,
                CurrencySign = x.entry.CurrencySign,
            }).ToList();
        }

        /// <summary>
        /// Bütün aktiv menyu sətirlərinin normalizə edilmiş proyeksiyası (120s cache).
        /// Discoverable filtri axtarış anında tətbiq olunur ki, tenant siyahısı
        /// dəyişəndə indeks etibarsız olmasın.
        /// </summary>
        private async Task<List<MenuIndexEntry>> GetMenuIndexAsync(CancellationToken ct)
        {
            if (_cache.TryGetValue(MenuIndexCacheKey, out List<MenuIndexEntry>? cached) && cached is not null)
                return cached;

            var rows = await _db.MenuItems
                .AsNoTracking()
                .Where(m => m.IsActive && m.MenuCategory.IsActive)
                .Select(m => new
                {
                    m.TenantId,
                    m.BranchId,
                    CatTenantId = m.MenuCategory.TenantId,
                    CatBranchId = m.MenuCategory.BranchId,
                    CatAz = m.MenuCategory.AzName,
                    CatEn = m.MenuCategory.EnName,
                    CatRu = m.MenuCategory.RuName,
                    m.AzName, m.EnName, m.RuName,
                    m.AzDescription, m.EnDescription, m.RuDescription,
                    m.Price, m.DiscountPrice, m.CurrencySign
                })
                .ToListAsync(ct);

            var list = new List<MenuIndexEntry>(rows.Count);
            foreach (var r in rows)
            {
                var dishName = FirstNonEmpty(r.AzName, r.EnName, r.RuName);
                var names = new[] { r.AzName, r.EnName, r.RuName }
                    .Where(s => !string.IsNullOrWhiteSpace(s)).ToArray();
                var descs = new[] { r.AzDescription, r.EnDescription, r.RuDescription }
                    .Where(s => !string.IsNullOrWhiteSpace(s)).ToArray();

                var nameHay = Normalize(string.Join(" ", names));
                var categoryName = FirstNonEmpty(r.CatAz, r.CatEn, r.CatRu);
                list.Add(new MenuIndexEntry
                {
                    EffTenantId = r.TenantId ?? r.CatTenantId,
                    SpecificBranchId = r.BranchId ?? r.CatBranchId,
                    DishName = dishName,
                    Description = FirstNonEmptyOrNull(r.AzDescription, r.EnDescription, r.RuDescription),
                    Price = r.Price,
                    DiscountPrice = r.DiscountPrice,
                    CurrencySign = r.CurrencySign,
                    CategoryName = categoryName,
                    CategoryNorm = Normalize(string.Join(" ", new[] { r.CatAz, r.CatEn, r.CatRu }
                        .Where(s => !string.IsNullOrWhiteSpace(s)))),
                    NameKey = Normalize(dishName),
                    NameHay = nameHay,
                    FullHay = descs.Length == 0
                        ? nameHay
                        : nameHay + " " + Normalize(string.Join(" ", descs))
                });
            }

            _cache.Set(MenuIndexCacheKey, list, MenuIndexTtl);
            return list;
        }

        private async Task<List<Branch>> GetBranchesAsync(CancellationToken ct)
        {
            if (_cache.TryGetValue(BranchCacheKey, out List<Branch>? cached) && cached is not null)
                return cached;
            var list = await _branchRepository.GetDiscoverableAsync(ct);
            _cache.Set(BranchCacheKey, list, CacheTtl);
            return list;
        }

        /// <summary>
        /// Filial adı sorğuya uyğundurmu: tam ad contains VƏ YA sorğunun bütün
        /// ≥3 hərfli sözləri (AppDisplayName/Name/CompanyName) haystack-dədir.
        /// </summary>
        internal static bool BranchMatchesPlace(Branch b, string placeNorm)
        {
            var hay = Normalize(string.Join(" ", new[]
            {
                b.AppDisplayName, b.Name, b.Tenant?.CompanyName
            }.Where(s => !string.IsNullOrWhiteSpace(s))));
            if (hay.Contains(placeNorm, StringComparison.Ordinal)) return true;
            var words = placeNorm.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Where(w => w.Length >= 3).ToArray();
            return words.Length > 0 && words.All(w => hay.Contains(w, StringComparison.Ordinal));
        }

        /// <summary>
        /// `get_place_menu` tool-u: konkret məkanın kateqoriya icmalı və ya bir
        /// kateqoriyanın itemləri. Məkan tapılmasa null.
        /// </summary>
        public async Task<object?> GetPlaceMenuAsync(string placeName, string? categoryName, CancellationToken ct = default)
        {
            var placeNorm = Normalize(placeName ?? string.Empty);
            if (placeNorm.Length == 0) return null;

            var branches = await GetBranchesAsync(ct);
            var matches = branches.Where(b => BranchMatchesPlace(b, placeNorm)).ToList();
            if (matches.Count == 0) return null;

            // Ən yaxşı uyğunluq: dəqiq ad bərabərliyi > əsas filial.
            var branch = matches
                .OrderByDescending(b => Normalize(b.AppDisplayName ?? b.Name ?? string.Empty) == placeNorm)
                .ThenByDescending(b => b.IsMainBranch)
                .First();

            var index = await GetMenuIndexAsync(ct);

            // Filialın sətirləri: spesifik sətirlər + həmin filialda override olunmayan tenant-wide sətirlər.
            var tenantRows = index.Where(e => e.EffTenantId == branch.TenantId).ToList();
            var specificKeys = tenantRows
                .Where(e => e.SpecificBranchId == branch.Id)
                .Select(e => e.NameKey)
                .ToHashSet();
            var rows = tenantRows.Where(e =>
                e.SpecificBranchId == branch.Id ||
                (e.SpecificBranchId is null && !specificKeys.Contains(e.NameKey))).ToList();

            if (rows.Count == 0) return null;

            var placeInfo = MapPlace(branch, null);

            var catNorm = Normalize(categoryName ?? string.Empty);
            if (catNorm.Length > 0)
            {
                var catWords = catNorm.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                    .Where(w => w.Length >= 3).ToArray();
                var items = rows.Where(e =>
                        e.CategoryNorm.Contains(catNorm, StringComparison.Ordinal) ||
                        (catWords.Length > 0 && catWords.All(w => e.CategoryNorm.Contains(w, StringComparison.Ordinal))))
                    .OrderBy(e => e.CategoryName).ThenBy(e => e.Price)
                    .Take(25)
                    .Select(e => new
                    {
                        dish = e.DishName,
                        category = e.CategoryName,
                        price = e.DiscountPrice > 0 ? e.DiscountPrice : e.Price,
                        regularPrice = e.DiscountPrice > 0 ? e.Price : (double?)null,
                        e.CurrencySign
                    })
                    .ToList();

                return new
                {
                    place = placeInfo.Name,
                    tenantId = placeInfo.TenantId,
                    branchId = placeInfo.BranchId,
                    category = categoryName,
                    items
                };
            }

            const int sampleSize = 5;
            var categories = rows
                .GroupBy(e => e.CategoryName)
                .Select(g => new
                {
                    category = g.Key,
                    itemCount = g.Count(),
                    minPrice = Math.Round(g.Min(e => e.DiscountPrice > 0 ? e.DiscountPrice : e.Price), 2),
                    maxPrice = Math.Round(g.Max(e => e.DiscountPrice > 0 ? e.DiscountPrice : e.Price), 2),
                    // Model tam siyahı üçün categoryName ilə get_place_menu-nu təkrar çağırmalıdır.
                    hasMore = g.Count() > sampleSize,
                    sample = g.Take(sampleSize).Select(e => new
                    {
                        dish = e.DishName,
                        price = e.DiscountPrice > 0 ? e.DiscountPrice : e.Price
                    }).ToList()
                })
                .OrderBy(c => c.category)
                .Take(30)
                .ToList();

            return new
            {
                place = placeInfo.Name,
                tenantId = placeInfo.TenantId,
                branchId = placeInfo.BranchId,
                totalItems = rows.Count,
                currencySign = rows.Select(e => e.CurrencySign)
                    .FirstOrDefault(s => !string.IsNullOrWhiteSpace(s)),
                categories
            };
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

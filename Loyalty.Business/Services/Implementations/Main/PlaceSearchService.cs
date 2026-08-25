using Loyalty.Business.Dtos.Chatbot;
using Loyalty.Business.Services.Abstractions.Main;
using Loyalty.DataAccess.Entities.Main;
using Loyalty.DataAccess.Repositories.Abstractions.Main;
using Microsoft.Extensions.Caching.Memory;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Loyalty.Business.Services.Implementations.Main
{
    public class PlaceSearchService : IPlaceSearchService
    {
        private readonly IBranchRepository _branchRepository;
        private readonly IMemoryCache _cache;
        private const string CacheKey = "assistant:discoverable-branches";
        private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(60);

        public PlaceSearchService(IBranchRepository branchRepository, IMemoryCache cache)
        {
            _branchRepository = branchRepository;
            _cache = cache;
        }

        public async Task<List<PlaceResultDto>> SearchAsync(PlaceSearchQuery query, CancellationToken ct = default)
        {
            var branches = await GetBranchesAsync(ct);

            // Sorğu tokenləri: keywords + categoryName + city — hamısı OR-match, çoxlu uyğunluq = yüksək bal.
            var tokens = new List<string>();
            if (query.Keywords != null) tokens.AddRange(query.Keywords);
            if (!string.IsNullOrWhiteSpace(query.CategoryName)) tokens.Add(query.CategoryName!);
            if (!string.IsNullOrWhiteSpace(query.City)) tokens.Add(query.City!);
            var normTokens = tokens
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .Select(Normalize)
                .Where(t => t.Length > 0)
                .Distinct()
                .ToList();

            bool hasGeo = query.Lat.HasValue && query.Lng.HasValue;
            bool geoFilter = hasGeo && query.RadiusKm.HasValue;

            // Keyword YOX + geo filtri YOX → hər şeyi dumping etmə (boş qaytar).
            if (normTokens.Count == 0 && !geoFilter)
                return new List<PlaceResultDto>();

            var scored = new List<(Branch branch, int score, double? distanceKm)>();

            foreach (var b in branches)
            {
                var haystack = BuildHaystack(b);

                int score = normTokens.Count == 0
                    ? 1 // yalnız geo — hamısı bərabər bal
                    : normTokens.Count(tok => haystack.Contains(tok, StringComparison.Ordinal));

                if (normTokens.Count > 0 && score == 0)
                    continue; // heç bir keyword uyğun gəlmədi

                double? distanceKm = null;
                if (hasGeo && b.Latitude.HasValue && b.Longitude.HasValue)
                {
                    distanceKm = Haversine(query.Lat!.Value, query.Lng!.Value,
                        (double)b.Latitude.Value, (double)b.Longitude.Value);
                }

                if (geoFilter)
                {
                    // Yaxınlıq tələbi var: koordinatı olmayan və ya radiusdan kənar filialları at.
                    if (distanceKm is null || distanceKm > query.RadiusKm!.Value)
                        continue;
                }

                scored.Add((b, score, distanceKm));
            }

            IEnumerable<(Branch branch, int score, double? distanceKm)> ordered = geoFilter
                ? scored.OrderBy(x => x.distanceKm ?? double.MaxValue).ThenByDescending(x => x.score)
                : scored.OrderByDescending(x => x.score).ThenBy(x => x.distanceKm ?? double.MaxValue);

            int take = Math.Clamp(query.Take <= 0 ? 8 : query.Take, 1, 20);

            return ordered.Take(take).Select(x => Map(x.branch, x.distanceKm)).ToList();
        }

        private async Task<List<Branch>> GetBranchesAsync(CancellationToken ct)
        {
            if (_cache.TryGetValue(CacheKey, out List<Branch>? cached) && cached is not null)
                return cached;

            var list = await _branchRepository.GetDiscoverableAsync(ct);
            _cache.Set(CacheKey, list, CacheTtl);
            return list;
        }

        private static string BuildHaystack(Branch b)
        {
            var t = b.Tenant;
            var parts = new[]
            {
                b.Keywords, b.Name, b.Address, b.AppDisplayName,
                t?.Keywords, t?.CompanyName, t?.Branding?.Description,
                t?.Category?.AzName, t?.Category?.EnName, t?.Category?.RuName
            };
            return Normalize(string.Join(" ", parts.Where(p => !string.IsNullOrWhiteSpace(p))));
        }

        private static PlaceResultDto Map(Branch b, double? distanceKm)
        {
            var t = b.Tenant!;
            var name = !string.IsNullOrWhiteSpace(b.AppDisplayName)
                ? b.AppDisplayName!
                : (b.IsMainBranch ? t.CompanyName : $"{t.CompanyName} — {b.Name}");

            bool inApp = t.IsHasMenu && !string.IsNullOrWhiteSpace(t.Subdomain);
            var combinedKeywords = string.Join(", ",
                new[] { t.Keywords, b.Keywords }.Where(k => !string.IsNullOrWhiteSpace(k)));

            return new PlaceResultDto
            {
                TenantId = t.Id,
                BranchId = b.Id,
                Name = name,
                CategoryName = t.Category?.AzName ?? t.Category?.EnName,
                Address = b.Address,
                DistanceKm = distanceKm.HasValue ? Math.Round(distanceKm.Value, 1) : (double?)null,
                Keywords = string.IsNullOrWhiteSpace(combinedKeywords) ? null : combinedKeywords,
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

        private static string Normalize(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return string.Empty;
            s = s.ToLowerInvariant().Trim();
            // AZ diakritikləri sadələşdir ki, "qelyan" ≈ "qəlyan" tuta bilsin.
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

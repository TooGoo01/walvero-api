using Loyalty.DataAccess.Entities.Main;

namespace Loyalty.Business.Helpers
{
    /// <summary>Böyük dairə məsafəsi (km).</summary>
    public static class GeoMath
    {
        private const double EarthRadiusKm = 6371.0;

        public static double HaversineKm(double lat1, double lng1, double lat2, double lng2)
        {
            static double Rad(double d) => d * Math.PI / 180.0;
            var dLat = Rad(lat2 - lat1);
            var dLng = Rad(lng2 - lng1);
            var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                    Math.Cos(Rad(lat1)) * Math.Cos(Rad(lat2)) * Math.Sin(dLng / 2) * Math.Sin(dLng / 2);
            return EarthRadiusKm * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        }
    }

    /// <summary>
    /// AI xarici məkan (AiReferencePlace) axtarışının sıralaması — pure, test olunur.
    /// Açar söz skoru = haystack-də (ad, kateqoriya, şəhər, ərazi, ünvan, keywords, description, MenuJson) neçə token var.
    /// Geo (lat/lng + radius) verilibsə: koordinatsız və radiusdan kənar məkanlar atılır; açar söz boşdursa
    /// ən yaxın məkanlar qaytarılır ("yaxınlıqda kafe"). Sıra: skor ↓, məsafə ↑, ad.
    /// </summary>
    public static class ReferencePlaceRanking
    {
        public sealed record Hit(AiReferencePlace Place, int Score, double? DistanceKm);

        public static List<Hit> Rank(
            IEnumerable<AiReferencePlace> places,
            IEnumerable<string?> tokens,
            double? lat, double? lng, double? radiusKm,
            int take)
        {
            var normTokens = tokens
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .Select(t => Normalize(t!))
                .Where(t => t.Length > 0)
                .Distinct()
                .ToList();

            var hasOrigin = lat.HasValue && lng.HasValue;
            var geoFilter = hasOrigin && radiusKm is > 0;
            if (normTokens.Count == 0 && !geoFilter)
                return new List<Hit>();

            var hits = new List<Hit>();
            foreach (var p in places)
            {
                double? distance = null;
                if (hasOrigin && p.Latitude.HasValue && p.Longitude.HasValue)
                    distance = GeoMath.HaversineKm(lat!.Value, lng!.Value, (double)p.Latitude.Value, (double)p.Longitude.Value);

                if (geoFilter && (distance is null || distance > radiusKm!.Value))
                    continue;

                var score = 0;
                if (normTokens.Count > 0)
                {
                    var haystack = Normalize(string.Join(" ", new[]
                    {
                        p.Name, p.Category, p.City, p.Area, p.Address, p.Keywords, p.Description, p.MenuJson
                    }.Where(s => !string.IsNullOrWhiteSpace(s))));
                    score = normTokens.Count(tok => haystack.Contains(tok, StringComparison.Ordinal));
                    if (score == 0) continue;
                }

                hits.Add(new Hit(p, score, distance is null ? null : Math.Round(distance.Value, 1)));
            }

            return hits
                .OrderByDescending(h => h.Score)
                .ThenBy(h => h.DistanceKm ?? double.MaxValue)
                .ThenBy(h => h.Place.Name, StringComparer.Ordinal)
                .Take(Math.Clamp(take <= 0 ? 8 : take, 1, 20))
                .ToList();
        }

        /// <summary>Kiçik hərf + AZ diakritiklər ASCII (ə→e, ğ→g, ı→i, ö→o, ü→u, ş→s, ç→c).</summary>
        public static string Normalize(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return string.Empty;
            var chars = s.ToLowerInvariant().Trim().Select(c => c switch
            {
                'ə' => 'e', 'ğ' => 'g', 'ı' => 'i', 'ö' => 'o', 'ü' => 'u', 'ş' => 's', 'ç' => 'c',
                _ => c
            }).ToArray();
            return new string(chars);
        }
    }
}

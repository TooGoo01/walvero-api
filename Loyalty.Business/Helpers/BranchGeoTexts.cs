using System.Text.Json;
using Loyalty.Business.Dtos.Get.Main.Branch;

namespace Loyalty.Business.Helpers
{
    /// <summary>
    /// Branch.GeoTextsJson — yaxınlıq bildirişi üçün başlıq + mətn cütləri (JSON massivi).
    /// Pass build / geofence zamanı təsadüfi bir cüt seçilir; boş siyahı → bugünkü default davranış.
    /// </summary>
    public static class BranchGeoTexts
    {
        public const int MaxItems = 10;
        public const int MaxTitleLength = 40;
        public const int MaxTextLength = 100;

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true
        };

        /// <summary>DB JSON → siyahı. Boş/yanlış JSON → boş siyahı (heç vaxt throw etmir).</summary>
        public static List<BranchGeoTextDto> Parse(string? json)
        {
            if (string.IsNullOrWhiteSpace(json)) return new List<BranchGeoTextDto>();
            try
            {
                return Normalize(JsonSerializer.Deserialize<List<BranchGeoTextDto>>(json, JsonOptions));
            }
            catch
            {
                return new List<BranchGeoTextDto>();
            }
        }

        /// <summary>Admin-dən gələn siyahı → DB JSON. Normalizasiyadan sonra boşdursa null.</summary>
        public static string? Serialize(IEnumerable<BranchGeoTextDto>? items)
        {
            var list = Normalize(items);
            return list.Count == 0 ? null : JsonSerializer.Serialize(list, JsonOptions);
        }

        /// <summary>Təsadüfi bir cüt; siyahı boşdursa null.</summary>
        public static BranchGeoTextDto? Pick(string? json)
        {
            var list = Parse(json);
            return list.Count == 0 ? null : list[Random.Shared.Next(list.Count)];
        }

        // trim, boş mətn atılır, uzunluq kəsilir, dublikat atılır, maks MaxItems
        private static List<BranchGeoTextDto> Normalize(IEnumerable<BranchGeoTextDto>? items)
        {
            var result = new List<BranchGeoTextDto>();
            if (items == null) return result;

            var seen = new HashSet<(string?, string)>();
            foreach (var item in items)
            {
                if (item == null) continue;
                var text = Cut(item.Text, MaxTextLength);
                if (text == null) continue;
                var title = Cut(item.Title, MaxTitleLength);
                if (!seen.Add((title, text))) continue;

                result.Add(new BranchGeoTextDto { Title = title, Text = text });
                if (result.Count == MaxItems) break;
            }
            return result;
        }

        private static string? Cut(string? value, int max)
        {
            var t = value?.Trim();
            if (string.IsNullOrEmpty(t)) return null;
            return t.Length > max ? t[..max].TrimEnd() : t;
        }
    }
}

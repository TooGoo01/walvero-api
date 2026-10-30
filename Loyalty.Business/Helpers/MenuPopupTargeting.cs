using System.Text.Json;

namespace Loyalty.Business.Helpers
{
    /// <summary>
    /// Menyu popup-unun filial hədəfi (MenuPopup.BranchIdsJson). null/boş = bütün filiallar.
    /// Filialsız ümumi menyu linkində (/menu) yalnız bütün filiallara aid popup-lar görünür. Pure — test olunur.
    /// </summary>
    public static class MenuPopupTargeting
    {
        public static List<int> Parse(string? json)
        {
            if (string.IsNullOrWhiteSpace(json)) return new List<int>();
            try { return (JsonSerializer.Deserialize<List<int>>(json) ?? new List<int>()).Where(i => i > 0).Distinct().ToList(); }
            catch { return new List<int>(); }
        }

        /// <summary>Boş siyahı → null (bütün filiallar).</summary>
        public static string? Serialize(IEnumerable<int>? branchIds)
        {
            var ids = branchIds?.Where(i => i > 0).Distinct().OrderBy(i => i).ToList();
            return ids == null || ids.Count == 0 ? null : JsonSerializer.Serialize(ids);
        }

        public static bool AppliesTo(string? branchIdsJson, int? branchId)
        {
            var ids = Parse(branchIdsJson);
            if (ids.Count == 0) return true;
            return branchId.HasValue && ids.Contains(branchId.Value);
        }
    }
}

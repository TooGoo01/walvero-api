using Loyalty.DataAccess.Entities.Main;

namespace Loyalty.Business.Helpers
{
    /// <summary>Məhsul / qrup / kateqoriya üzrə qayda (istisna və ya qazanc qaydası) — dəyəri T.</summary>
    public sealed record ProductRuleEntry<T>(PosEarnExclusionKind Kind, string ExternalId, string? Code, T Value);

    /// <summary>
    /// POS sifariş sətri → qayda lookup-u (bonus istisnaları və məhsul qazanc qaydaları üçün ortaq).
    /// Ən spesifik qayda qalib gəlir: Product → ən dərin Group (parentGroup zənciri) → Category.
    /// Qrup qaydaları yalnız nomenklatura surəti ilə həll olunur; kateqoriya həm surətdən, həm də sətrin plugin CategoryId-sindən.
    /// </summary>
    public sealed class ProductRuleIndex<T> where T : class
    {
        public static readonly ProductRuleIndex<T> Empty = new(
            new Dictionary<string, T>(StringComparer.OrdinalIgnoreCase),
            new Dictionary<string, T>(StringComparer.OrdinalIgnoreCase),
            new Dictionary<string, T>(StringComparer.OrdinalIgnoreCase));

        private readonly Dictionary<string, T> _byProductId;
        private readonly Dictionary<string, T> _byCode;
        private readonly Dictionary<string, T> _byCategoryId;

        private ProductRuleIndex(Dictionary<string, T> byProductId, Dictionary<string, T> byCode, Dictionary<string, T> byCategoryId)
        {
            _byProductId = byProductId;
            _byCode = byCode;
            _byCategoryId = byCategoryId;
        }

        public bool IsEmpty => _byProductId.Count == 0 && _byCode.Count == 0 && _byCategoryId.Count == 0;

        public static ProductRuleIndex<T> Build(IEnumerable<ProductRuleEntry<T>> entries, NomenclatureSnapshot? nomenclature)
        {
            var cmp = StringComparer.OrdinalIgnoreCase;
            var byProductId = new Dictionary<string, T>(cmp);
            var byCode = new Dictionary<string, T>(cmp);
            var byCategoryId = new Dictionary<string, T>(cmp);

            var productRules = new Dictionary<string, T>(cmp);
            var productRulesByCode = new Dictionary<string, T>(cmp);
            var groupRules = new Dictionary<string, T>(cmp);

            foreach (var e in entries)
            {
                var id = (e.ExternalId ?? "").Trim();
                if (id.Length == 0) continue;
                switch (e.Kind)
                {
                    case PosEarnExclusionKind.Product:
                        productRules[id] = e.Value;
                        var code = (e.Code ?? "").Trim();
                        if (code.Length > 0) productRulesByCode[code] = e.Value;
                        // Əl ilə daxil edilmiş qeyri-GUID ID kod kimi də uyğunlaşsın (iiko ProductNumber)
                        if (!Guid.TryParse(id, out _)) productRulesByCode.TryAdd(id, e.Value);
                        break;
                    case PosEarnExclusionKind.Group:
                        groupRules[id] = e.Value;
                        break;
                    case PosEarnExclusionKind.Category:
                        byCategoryId[id] = e.Value;
                        break;
                }
            }

            foreach (var (id, v) in productRules) byProductId[id] = v;
            foreach (var (code, v) in productRulesByCode) byCode[code] = v;

            if (nomenclature != null && (groupRules.Count > 0 || byCategoryId.Count > 0 || productRules.Count > 0))
            {
                var parentOf = new Dictionary<string, string?>(cmp);
                foreach (var g in nomenclature.Groups) parentOf.TryAdd(g.Id, g.ParentGroup);

                foreach (var p in nomenclature.Products)
                {
                    var v = ResolveProduct(p, productRules, productRulesByCode, groupRules, byCategoryId, parentOf);
                    if (v == null) continue;
                    byProductId.TryAdd(p.Id, v);
                    var code = (p.Code ?? "").Trim();
                    if (code.Length > 0) byCode.TryAdd(code, v);
                }
            }

            return new ProductRuleIndex<T>(byProductId, byCode, byCategoryId);
        }

        private static T? ResolveProduct(
            NomenclatureProduct p,
            Dictionary<string, T> productRules,
            Dictionary<string, T> productRulesByCode,
            Dictionary<string, T> groupRules,
            Dictionary<string, T> categoryRules,
            Dictionary<string, string?> parentOf)
        {
            if (productRules.TryGetValue(p.Id, out var v)) return v;
            var code = (p.Code ?? "").Trim();
            if (code.Length > 0 && productRulesByCode.TryGetValue(code, out v)) return v;

            if (groupRules.Count > 0)
            {
                var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var g = p.ParentGroup;
                while (!string.IsNullOrEmpty(g) && visited.Add(g))
                {
                    if (groupRules.TryGetValue(g, out v)) return v;
                    g = parentOf.TryGetValue(g, out var parent) ? parent : null;
                }
            }

            if (!string.IsNullOrEmpty(p.CategoryId) && categoryRules.TryGetValue(p.CategoryId, out v)) return v;
            return null;
        }

        /// <summary>Sifariş sətri üçün qayda: ProductId → kod (ProductNumber) → plugin CategoryId.</summary>
        public T? Lookup(string? productId, string? productNumber, string? categoryId)
        {
            var pid = (productId ?? "").Trim();
            if (pid.Length > 0 && _byProductId.TryGetValue(pid, out var v)) return v;
            var pnum = (productNumber ?? "").Trim();
            if (pnum.Length > 0 && _byCode.TryGetValue(pnum, out v)) return v;
            var cat = (categoryId ?? "").Trim();
            if (cat.Length > 0 && _byCategoryId.TryGetValue(cat, out v)) return v;
            return null;
        }
    }
}

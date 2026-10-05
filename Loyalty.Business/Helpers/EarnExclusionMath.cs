using System.Text.Json;
using Loyalty.DataAccess.Entities.Main;

namespace Loyalty.Business.Helpers
{
    /// <summary>Bir məhsulun effektiv bonus bloku və onun mənbəyi (hansı qaydadan gəldiyi).</summary>
    public sealed record EarnBlock(bool BlockCashback, bool BlockStamps, PosEarnExclusionKind SourceKind, string SourceName)
    {
        public bool Any => BlockCashback || BlockStamps;
    }

    /// <summary>Super admin qaydası (PosEarnExclusion-un pure surəti).</summary>
    public sealed record EarnExclusionRule(
        PosEarnExclusionKind Kind, string ExternalId, string? Code, string? Name, bool BlockCashback, bool BlockStamps);

    public sealed record NomenclatureProduct(string Id, string? Code, string? Name, string? ParentGroup, string? CategoryId, string? Type);
    public sealed record NomenclatureGroup(string Id, string? Name, string? ParentGroup);
    public sealed record NomenclatureCategory(string Id, string? Name);

    /// <summary>iiko nomenklaturasının yığcam surəti (bir və ya bir neçə org birləşdirilmiş).</summary>
    public sealed class NomenclatureSnapshot
    {
        public List<NomenclatureProduct> Products { get; init; } = new();
        public List<NomenclatureGroup> Groups { get; init; } = new();
        public List<NomenclatureCategory> Categories { get; init; } = new();

        public static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

        /// <summary>Org-ların surətlərini birləşdirir (iiko şəbəkəsində GUID-lər org-lar arası eynidir; ilk gələn qalır).</summary>
        public static NomenclatureSnapshot Merge(IEnumerable<NomenclatureSnapshot> parts)
        {
            var products = new Dictionary<string, NomenclatureProduct>(StringComparer.OrdinalIgnoreCase);
            var groups = new Dictionary<string, NomenclatureGroup>(StringComparer.OrdinalIgnoreCase);
            var categories = new Dictionary<string, NomenclatureCategory>(StringComparer.OrdinalIgnoreCase);
            foreach (var p in parts)
            {
                foreach (var x in p.Products) products.TryAdd(x.Id, x);
                foreach (var x in p.Groups) groups.TryAdd(x.Id, x);
                foreach (var x in p.Categories) categories.TryAdd(x.Id, x);
            }
            return new NomenclatureSnapshot
            {
                Products = products.Values.ToList(),
                Groups = groups.Values.ToList(),
                Categories = categories.Values.ToList()
            };
        }
    }

    /// <summary>
    /// İnteqrasiya üçün hazır lookup: məhsul GUID / kod / kateqoriya → effektiv blok.
    /// Ən spesifik qayda qalib gəlir: Product → ən dərin Group (parentGroup zənciri) → Category.
    /// Hər iki flag-ı false olan nəticə də saxlanır (ağ siyahı) — daha ümumi fallback-ı (kod/kateqoriya) dayandırır.
    /// </summary>
    public sealed class EarnExclusionMap
    {
        public static readonly EarnExclusionMap Empty = new(
            new Dictionary<string, EarnBlock>(), new Dictionary<string, EarnBlock>(), new Dictionary<string, EarnBlock>());

        private readonly Dictionary<string, EarnBlock> _byProductId;
        private readonly Dictionary<string, EarnBlock> _byCode;
        private readonly Dictionary<string, EarnBlock> _byCategoryId;

        private EarnExclusionMap(
            Dictionary<string, EarnBlock> byProductId, Dictionary<string, EarnBlock> byCode, Dictionary<string, EarnBlock> byCategoryId)
        {
            _byProductId = byProductId;
            _byCode = byCode;
            _byCategoryId = byCategoryId;
        }

        public bool IsEmpty => _byProductId.Count == 0 && _byCode.Count == 0 && _byCategoryId.Count == 0;

        public static EarnExclusionMap Build(IEnumerable<EarnExclusionRule> rules, NomenclatureSnapshot? nomenclature)
        {
            var cmp = StringComparer.OrdinalIgnoreCase;
            var byProductId = new Dictionary<string, EarnBlock>(cmp);
            var byCode = new Dictionary<string, EarnBlock>(cmp);
            var byCategoryId = new Dictionary<string, EarnBlock>(cmp);

            var productRules = new Dictionary<string, EarnBlock>(cmp);
            var productRulesByCode = new Dictionary<string, EarnBlock>(cmp);
            var groupRules = new Dictionary<string, EarnBlock>(cmp);

            foreach (var r in rules)
            {
                var id = (r.ExternalId ?? "").Trim();
                if (id.Length == 0) continue;
                var block = new EarnBlock(r.BlockCashback, r.BlockStamps, r.Kind, string.IsNullOrWhiteSpace(r.Name) ? id : r.Name.Trim());
                switch (r.Kind)
                {
                    case PosEarnExclusionKind.Product:
                        productRules[id] = block;
                        var code = (r.Code ?? "").Trim();
                        if (code.Length > 0) productRulesByCode[code] = block;
                        // Əl ilə daxil edilmiş qeyri-GUID ID kod kimi də uyğunlaşsın (PosProductEarnRule kimi)
                        if (!Guid.TryParse(id, out _)) productRulesByCode.TryAdd(id, block);
                        break;
                    case PosEarnExclusionKind.Group:
                        groupRules[id] = block;
                        break;
                    case PosEarnExclusionKind.Category:
                        byCategoryId[id] = block;
                        break;
                }
            }

            foreach (var (id, b) in productRules) byProductId[id] = b;
            foreach (var (code, b) in productRulesByCode) byCode[code] = b;

            if (nomenclature != null && (groupRules.Count > 0 || byCategoryId.Count > 0 || productRules.Count > 0))
            {
                var parentOf = new Dictionary<string, string?>(cmp);
                foreach (var g in nomenclature.Groups) parentOf.TryAdd(g.Id, g.ParentGroup);

                foreach (var p in nomenclature.Products)
                {
                    var block = ResolveProduct(p, productRules, productRulesByCode, groupRules, byCategoryId, parentOf);
                    if (block == null) continue;
                    byProductId.TryAdd(p.Id, block);
                    var code = (p.Code ?? "").Trim();
                    if (code.Length > 0) byCode.TryAdd(code, block);
                }
            }

            return new EarnExclusionMap(byProductId, byCode, byCategoryId);
        }

        private static EarnBlock? ResolveProduct(
            NomenclatureProduct p,
            Dictionary<string, EarnBlock> productRules,
            Dictionary<string, EarnBlock> productRulesByCode,
            Dictionary<string, EarnBlock> groupRules,
            Dictionary<string, EarnBlock> categoryRules,
            Dictionary<string, string?> parentOf)
        {
            if (productRules.TryGetValue(p.Id, out var b)) return b;
            var code = (p.Code ?? "").Trim();
            if (code.Length > 0 && productRulesByCode.TryGetValue(code, out b)) return b;

            if (groupRules.Count > 0)
            {
                var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var g = p.ParentGroup;
                while (!string.IsNullOrEmpty(g) && visited.Add(g))
                {
                    if (groupRules.TryGetValue(g, out b)) return b;
                    g = parentOf.TryGetValue(g, out var parent) ? parent : null;
                }
            }

            if (!string.IsNullOrEmpty(p.CategoryId) && categoryRules.TryGetValue(p.CategoryId, out b)) return b;
            return null;
        }

        /// <summary>Sifariş sətri üçün qayda (ağ siyahı daxil): ProductId → kod (ProductNumber) → plugin CategoryId.</summary>
        public EarnBlock? Lookup(string? productId, string? productNumber, string? categoryId)
        {
            var pid = (productId ?? "").Trim();
            if (pid.Length > 0 && _byProductId.TryGetValue(pid, out var b)) return b;
            var pnum = (productNumber ?? "").Trim();
            if (pnum.Length > 0 && _byCode.TryGetValue(pnum, out b)) return b;
            var cat = (categoryId ?? "").Trim();
            if (cat.Length > 0 && _byCategoryId.TryGetValue(cat, out b)) return b;
            return null;
        }

        /// <summary>Yalnız real blok (ən azı bir flag) — ağ siyahı nəticəsi null qaytarır.</summary>
        public EarnBlock? Blocked(string? productId, string? productNumber, string? categoryId)
            => Lookup(productId, productNumber, categoryId) is { Any: true } b ? b : null;
    }

    public sealed record EarnLine(string? ProductId, string? ProductNumber, string? CategoryId, string? Name, decimal Amount, decimal Sum);

    /// <summary>Discounts[].DiscountByItem sətri (bütün endirim növləri).</summary>
    public sealed record EarnItemDiscount(string? ProductId, decimal DiscountSum);

    public sealed record EarnBlockedLine(string? ProductId, string? Name, decimal Amount, decimal Net, EarnBlock Block);

    public sealed class EarnBaseResult
    {
        /// <summary>Məbləğ bonusunun (cashback %, xal/₼, xoş gəldin %) bazası.</summary>
        public decimal CashbackBase { get; init; }
        /// <summary>Bonusdan çıxılan xalis məbləğ (endirim və proporsional payla).</summary>
        public decimal ExcludedNet { get; init; }
        public List<EarnBlockedLine> BlockedLines { get; init; } = new();

        public bool AnyBlocked => BlockedLines.Count > 0;
    }

    public sealed record EarnRuleHit(int StepsPerUnit, decimal PointsPerUnit);

    public sealed class StampEarnResult
    {
        public int Steps { get; set; }
        public decimal Points { get; set; }
        public bool AnyRuleMatched { get; set; }
        /// <summary>Qaydası olan, amma BlockStamps səbəbindən sayılmayan ədəd.</summary>
        public decimal SkippedUnits { get; set; }
    }

    public static class EarnExclusionMath
    {
        /// <summary>
        /// Bloklanmış (BlockCashback) sətirlərin xalis məbləğini sifariş bazasından çıxarır.
        /// Sətir xalisi = Σ Sum − DiscountByItem (ProductId üzrə); qalan sifariş səviyyəli endirim (TotalSum &lt; Σ xalis)
        /// bütün sətirlərə proporsional paylanır. Wallet ödənişi uyğun bazaya proporsional düşür.
        /// Blok yoxdursa nəticə köhnə düsturla eynidir: Progress → TotalSum, digər → max(0, TotalSum − wallet).
        /// </summary>
        public static EarnBaseResult Compute(
            IReadOnlyList<EarnLine> items,
            IReadOnlyList<EarnItemDiscount> itemDiscounts,
            decimal totalSum,
            decimal walletRedeemSum,
            bool isProgress,
            EarnExclusionMap map)
        {
            var legacyBase = isProgress ? totalSum : Math.Max(0m, totalSum - walletRedeemSum);
            if (map.IsEmpty || items.Count == 0)
                return new EarnBaseResult { CashbackBase = legacyBase };

            var blocks = items.Select(i => map.Blocked(i.ProductId, i.ProductNumber, i.CategoryId)).ToList();
            if (blocks.All(b => b == null))
                return new EarnBaseResult { CashbackBase = legacyBase };

            // Sətirləri ProductId üzrə qruplaşdır (DiscountByItem yalnız ProductId verir); ID-siz sətir öz qrupudur.
            string KeyOf(int idx)
            {
                var pid = (items[idx].ProductId ?? "").Trim();
                return pid.Length > 0 ? pid.ToLowerInvariant() : "#" + idx;
            }

            var grossByKey = new Dictionary<string, decimal>();
            for (int i = 0; i < items.Count; i++)
            {
                var k = KeyOf(i);
                grossByKey[k] = grossByKey.GetValueOrDefault(k) + Math.Max(0m, items[i].Sum);
            }

            var discByKey = new Dictionary<string, decimal>();
            foreach (var d in itemDiscounts)
            {
                var pid = (d.ProductId ?? "").Trim();
                if (pid.Length == 0) continue;
                var k = pid.ToLowerInvariant();
                discByKey[k] = discByKey.GetValueOrDefault(k) + d.DiscountSum;
            }

            var netByKey = grossByKey.ToDictionary(kv => kv.Key, kv => Math.Max(0m, kv.Value - discByKey.GetValueOrDefault(kv.Key)));
            var totalNet = netByKey.Values.Sum();
            var factor = totalNet > 0m && totalSum < totalNet ? Math.Max(0m, totalSum) / totalNet : 1m;

            var blockedLines = new List<EarnBlockedLine>();
            decimal excluded = 0m;
            for (int i = 0; i < items.Count; i++)
            {
                var block = blocks[i];
                if (block == null) continue;
                var k = KeyOf(i);
                var gross = grossByKey[k];
                var lineNet = gross > 0m ? netByKey[k] * (Math.Max(0m, items[i].Sum) / gross) * factor : 0m;
                if (block.BlockCashback) excluded += lineNet;
                blockedLines.Add(new EarnBlockedLine(items[i].ProductId, items[i].Name, items[i].Amount, Round2(lineNet), block));
            }

            excluded = Math.Min(Math.Max(0m, excluded), Math.Max(0m, totalSum));
            if (excluded == 0m)
                return new EarnBaseResult { CashbackBase = legacyBase, BlockedLines = blockedLines };

            var eligible = Math.Max(0m, totalSum - excluded);
            decimal cashbackBase;
            if (isProgress)
                cashbackBase = eligible;
            else if (totalSum > 0m)
                cashbackBase = Math.Max(0m, eligible - walletRedeemSum * eligible / totalSum);
            else
                cashbackBase = 0m;

            return new EarnBaseResult
            {
                CashbackBase = Round2(cashbackBase),
                ExcludedNet = Round2(excluded),
                BlockedLines = blockedLines
            };
        }

        /// <summary>
        /// Məhsul bonusu (PosProductEarnRule) döngüsü. Qaydası olan sətir həmişə "uyğunlaşmış" sayılır (rejim seçimi
        /// dəyişmir — bloklanmış möhür məhsulu cashback fallback-ını açmasın), amma BlockStamps sətrin steps/points-i
        /// əlavə olunmur. Redeem olunmuş (loyalty endirimli) sətirlər əvvəlki kimi tam atlanır.
        /// </summary>
        public static StampEarnResult ApplyEarnRules(
            IReadOnlyList<EarnLine> items,
            Func<EarnLine, EarnRuleHit?> ruleFor,
            Func<EarnLine, bool> isRedeemed,
            EarnExclusionMap map)
        {
            var result = new StampEarnResult();
            foreach (var item in items)
            {
                if (string.IsNullOrWhiteSpace(item.ProductId) && string.IsNullOrWhiteSpace(item.ProductNumber)) continue;
                if (isRedeemed(item)) continue;
                var rule = ruleFor(item);
                if (rule == null) continue;
                result.AnyRuleMatched = true;
                if (map.Blocked(item.ProductId, item.ProductNumber, item.CategoryId)?.BlockStamps == true)
                {
                    result.SkippedUnits += item.Amount;
                    continue;
                }
                result.Steps += rule.StepsPerUnit * (int)item.Amount;
                result.Points += rule.PointsPerUnit * item.Amount;
            }
            return result;
        }

        /// <summary>Earn tx təsvirinə qısa əlavə (Transaction.Description ≤ 500).</summary>
        /// <param name="stampSkippedUnits">Earn rule-u olan, amma BlockStamps səbəbindən sayılmayan ədəd.</param>
        public static string DescribeSuffix(EarnBaseResult r, decimal stampSkippedUnits)
        {
            var inv = System.Globalization.CultureInfo.InvariantCulture;
            var parts = new List<string>();
            if (r.ExcludedNet > 0m) parts.Add($"bonussuz məhsullar −{r.ExcludedNet.ToString("0.##", inv)} ₼");
            if (stampSkippedUnits > 0m) parts.Add($"möhürsüz {stampSkippedUnits.ToString("0.##", inv)} əd.");
            return parts.Count == 0 ? string.Empty : " · " + string.Join(" · ", parts);
        }

        private static decimal Round2(decimal v) => Math.Round(v, 2, MidpointRounding.AwayFromZero);
    }
}

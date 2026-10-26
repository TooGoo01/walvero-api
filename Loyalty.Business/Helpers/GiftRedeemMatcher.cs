namespace Loyalty.Business.Helpers
{
    /// <summary>Hədiyyə: ad, qiymət, kassa endirimi, hədiyyə ola bilən kassa kodları (RewardPosProducts ∪ ExternalProductId).</summary>
    public sealed record GiftRewardInfo(int Id, string Name, int PointsCost, string? PosDiscountTypeId, IReadOnlyCollection<string> ProductCodes);

    public sealed record GiftOrderLine(string? ProductId, string? ProductNumber, decimal Amount);

    /// <summary>Sifarişin endirimi: növ + DiscountByItem sətirlərinin ProductId-ləri (GUID).</summary>
    public sealed record GiftOrderDiscount(string? DiscountTypeId, IReadOnlyList<string?> ItemProductIds);

    public sealed class GiftRedeemResult
    {
        /// <summary>Verilmiş hədiyyələr (sifarişdəki ilk görünmə sırası ilə).</summary>
        public List<(GiftRewardInfo Reward, int Count)> Rewards { get; } = new();
        /// <summary>Hədiyyə verilmiş məhsul açarları (ProductNumber, yoxdursa GUID) → ədəd — bu sətirlər qazancdan düşür.</summary>
        public Dictionary<string, int> RedeemedUnitsByProduct { get; } = new(StringComparer.OrdinalIgnoreCase);
        /// <summary>Hədiyyə endirimi var, amma heç bir hədiyyəyə uyğun gəlməyən məhsul açarları (log üçün).</summary>
        public List<string> UnmappedProductKeys { get; } = new();

        public int TotalUnits => Rewards.Sum(r => r.Count);
        public decimal TotalPointsCost => Rewards.Sum(r => (decimal)r.Reward.PointsCost * r.Count);

        internal void Add(GiftRewardInfo reward, int count)
        {
            if (count <= 0) return;
            var i = Rewards.FindIndex(x => x.Reward.Id == reward.Id);
            if (i >= 0) Rewards[i] = (reward, Rewards[i].Count + count);
            else Rewards.Add((reward, count));
        }
    }

    /// <summary>
    /// iiko OrderClosed: sifarişdə hansı hədiyyələr verilib. Pure — test olunur.
    /// 1) Endirim növü hədiyyəyə bağlıdırsa (Reward.PosDiscountTypeId) — həmin hədiyyə; say = DiscountByItem sətirləri,
    ///    sətir yoxdursa sifarişdəki hədiyyə məhsullarının ədədi, o da yoxdursa 1 (ölçülü "*" hədiyyələr yalnız belə tanınır).
    /// 2) Köhnə yol: Walvero hədiyyə endirimi (PosLoyaltyDiscountTypes) + məhsul kodu → hədiyyə; sətirsiz endirimdə sifarişdəki
    ///    hədiyyə məhsulları sayılır (yalnız heç bir sətir tapılmayıbsa).
    /// </summary>
    public static class GiftRedeemMatcher
    {
        public static GiftRedeemResult Match(
            IReadOnlyList<GiftOrderLine> items,
            IReadOnlyList<GiftOrderDiscount> discounts,
            IReadOnlySet<string> loyaltyDiscountTypeIds,
            IReadOnlyList<GiftRewardInfo> rewards)
        {
            var result = new GiftRedeemResult();
            var cmp = StringComparer.OrdinalIgnoreCase;

            // DiscountByItem yalnız GUID göndərir; hədiyyələr kassa koduna (ProductNumber) bağlıdır
            var numberByProductId = new Dictionary<string, string>(cmp);
            foreach (var it in items)
            {
                var gid = (it.ProductId ?? "").Trim();
                var num = (it.ProductNumber ?? "").Trim();
                if (gid.Length > 0 && num.Length > 0) numberByProductId[gid] = num;
            }
            var rewardByCode = new Dictionary<string, GiftRewardInfo>(cmp);
            foreach (var r in rewards)
                foreach (var code in r.ProductCodes.Where(c => !string.IsNullOrWhiteSpace(c)))
                    rewardByCode.TryAdd(code.Trim(), r);

            var typeRewardsWithoutItems = new List<GiftRewardInfo>();
            var legacyWithoutItems = false;

            foreach (var d in discounts)
            {
                var dtid = (d.DiscountTypeId ?? "").Trim();
                if (dtid.Length == 0) continue;
                var typeReward = rewards.FirstOrDefault(r => cmp.Equals((r.PosDiscountTypeId ?? "").Trim(), dtid));
                if (typeReward == null && !loyaltyDiscountTypeIds.Contains(dtid)) continue;

                var lines = d.ItemProductIds.Select(x => (x ?? "").Trim()).Where(x => x.Length > 0).ToList();
                if (lines.Count == 0)
                {
                    if (typeReward != null) typeRewardsWithoutItems.Add(typeReward);
                    else legacyWithoutItems = true;
                    continue;
                }
                foreach (var pid in lines)
                {
                    var key = numberByProductId.TryGetValue(pid, out var num) ? num : pid;
                    result.RedeemedUnitsByProduct[key] = result.RedeemedUnitsByProduct.GetValueOrDefault(key) + 1;
                    var reward = typeReward ?? (rewardByCode.TryGetValue(key, out var byCode) ? byCode : null);
                    if (reward != null) result.Add(reward, 1);
                    else result.UnmappedProductKeys.Add(key);
                }
            }

            // Bağlı endirim, amma sətirsiz: sifarişdəki həmin hədiyyənin məhsulları; yoxdursa bir hədiyyə
            foreach (var reward in typeRewardsWithoutItems.DistinctBy(r => r.Id))
            {
                var codes = reward.ProductCodes.ToHashSet(cmp);
                var units = 0;
                foreach (var it in items)
                {
                    var key = KeyOf(it, codes);
                    if (key == null) continue;
                    var n = Math.Max(1, (int)it.Amount);
                    units += n;
                    result.RedeemedUnitsByProduct[key] = result.RedeemedUnitsByProduct.GetValueOrDefault(key) + n;
                }
                result.Add(reward, Math.Max(1, units));
            }

            // Köhnə fallback (məs. "Донер в подарок"): Walvero hədiyyə endirimi sətirsiz gəlib, heç nə tapılmayıb
            if (legacyWithoutItems && result.RedeemedUnitsByProduct.Count == 0 && rewardByCode.Count > 0)
            {
                foreach (var it in items)
                {
                    var key = KeyOf(it, rewardByCode.Keys);
                    if (key == null) continue;
                    var n = Math.Max(1, (int)it.Amount);
                    result.RedeemedUnitsByProduct[key] = result.RedeemedUnitsByProduct.GetValueOrDefault(key) + n;
                    result.Add(rewardByCode[key], n);
                }
            }

            return result;
        }

        /// <summary>Sətrin hədiyyə açarı: əvvəl kassa kodu, sonra GUID (köhnə hədiyyələr GUID-lə bağlana bilər).</summary>
        private static string? KeyOf(GiftOrderLine it, IEnumerable<string> codes)
        {
            var set = codes as ISet<string> ?? codes.ToHashSet(StringComparer.OrdinalIgnoreCase);
            var pnum = (it.ProductNumber ?? "").Trim();
            if (pnum.Length > 0 && set.Contains(pnum)) return pnum;
            var pid = (it.ProductId ?? "").Trim();
            return pid.Length > 0 && set.Contains(pid) ? pid : null;
        }
    }
}

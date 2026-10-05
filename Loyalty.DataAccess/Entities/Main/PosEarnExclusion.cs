using Loyalty.DataAccess.Abstractions;
using Loyalty.DataAccess.Entities.Base;

namespace Loyalty.DataAccess.Entities.Main
{
    public enum PosEarnExclusionKind
    {
        Product = 1,
        Group = 2,     // iiko nomenklatura qrupu (alt-qruplar daxil)
        Category = 3   // iiko məhsul kateqoriyası (productCategory)
    }

    /// <summary>
    /// POS sifarişində bonus qazandırmayan məhsul / qrup / kateqoriya (super admin idarə edir).
    /// BlockCashback — məbləğ bonusu (cashback %, xal/₼, xoş gəldin bonusunun % hissəsi) bu sətirlərdən hesablanmır.
    /// BlockStamps — məhsul bonusu (PosProductEarnRule: möhür, məhsul xalı, "hər məhsul +1 xal") verilmir.
    /// Ən spesifik qayda qalib gəlir: Product → ən dərin Group → Category. Product/Group sətrində ikisi də false =
    /// yuxarı səviyyədəki bloku ləğv edir (ağ siyahı).
    /// </summary>
    public class PosEarnExclusion : EntityBase, IEntity
    {
        public int TenantIntegrationId { get; set; }
        public PosEarnExclusionKind Kind { get; set; }
        /// <summary>iiko GUID (məhsul / qrup / kateqoriya) və ya Monyo məhsul ID-si.</summary>
        public string ExternalId { get; set; } = string.Empty;
        /// <summary>Məhsul kodu (plugin ProductNumber) — yalnız Product.</summary>
        public string? Code { get; set; }
        public string? Name { get; set; }
        public bool BlockCashback { get; set; } = true;
        public bool BlockStamps { get; set; } = true;
        public string? Note { get; set; }

        public TenantIntegration TenantIntegration { get; set; } = null!;
    }
}

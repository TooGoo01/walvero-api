using Loyalty.DataAccess.Abstractions;
using Loyalty.DataAccess.Entities.Base;

namespace Loyalty.DataAccess.Entities.Main
{
    /// <summary>
    /// POS sifarişlərində görünmüş endirim növü (iiko plugin Discounts[]). Super admin hədiyyə (loyalty) endirimini
    /// GUID yazmadan bu siyahıdan seçir — iiko Cloud endirim siyahısı boş/əlçatmaz olanda da işləyir.
    /// </summary>
    public class PosObservedDiscount : EntityBase, IEntity
    {
        public int TenantIntegrationId { get; set; }
        public string DiscountTypeId { get; set; } = string.Empty;
        public string? Name { get; set; }
        public DateTime FirstSeenAtUtc { get; set; }
        public DateTime LastSeenAtUtc { get; set; }

        public TenantIntegration TenantIntegration { get; set; } = null!;
    }
}

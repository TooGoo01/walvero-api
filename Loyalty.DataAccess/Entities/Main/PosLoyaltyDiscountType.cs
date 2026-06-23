using Loyalty.DataAccess.Abstractions;
using Loyalty.DataAccess.Entities.Base;

namespace Loyalty.DataAccess.Entities.Main
{
    /// <summary>
    /// Admin-configured iiko DiscountTypeId values that represent loyalty reward redemption.
    /// When OrderClosed event arrives with a Discount whose DiscountTypeId matches, the items
    /// in DiscountByItem[] are treated as free rewards claimed by the customer.
    /// </summary>
    public class PosLoyaltyDiscountType : EntityBase, IEntity
    {
        public int TenantIntegrationId { get; set; }
        public string ExternalDiscountTypeId { get; set; } = string.Empty;
        public string? DiscountName { get; set; }

        public TenantIntegration TenantIntegration { get; set; } = null!;
    }
}

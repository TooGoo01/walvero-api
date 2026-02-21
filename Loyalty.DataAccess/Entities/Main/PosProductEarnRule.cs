using Loyalty.DataAccess.Abstractions;
using Loyalty.DataAccess.Entities.Base;

namespace Loyalty.DataAccess.Entities.Main
{
    /// <summary>
    /// Maps POS product IDs to steps/points earned per unit when customer buys that product.
    /// Used by collect (order closed) to add loyalty stamps/points.
    /// </summary>
    public class PosProductEarnRule : EntityBase, IEntity
    {
        public int TenantIntegrationId { get; set; }
        public string ExternalProductId { get; set; } = string.Empty;
        public string? ProductName { get; set; }
        public int StepsPerUnit { get; set; }
        public int PointsPerUnit { get; set; }

        public TenantIntegration TenantIntegration { get; set; } = null!;
    }
}

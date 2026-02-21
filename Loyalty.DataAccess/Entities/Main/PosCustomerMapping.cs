using Loyalty.DataAccess.Abstractions;
using Loyalty.DataAccess.Entities.Base;

namespace Loyalty.DataAccess.Entities.Main
{
    /// <summary>
    /// Maps POS (e.g. Monyo) customer ID to our Customer.
    /// Populated when syncing customers to POS.
    /// </summary>
    public class PosCustomerMapping : EntityBase, IEntity
    {
        public int TenantIntegrationId { get; set; }
        public string ExternalCustomerId { get; set; } = string.Empty;
        public int CustomerId { get; set; }

        public TenantIntegration TenantIntegration { get; set; } = null!;
        public Customer Customer { get; set; } = null!;
    }
}

using Loyalty.DataAccess.Abstractions;
using Loyalty.DataAccess.Entities.Base;

namespace Loyalty.DataAccess.Entities.Main
{
    /// <summary>
    /// Tenant-specific configuration for a POS integration (e.g. Monyo).
    /// Credentials and settings are per tenant; config is created after selecting tenant and integration type.
    /// </summary>
    public class TenantIntegration : EntityBase, IEntity
    {
        public int TenantId { get; set; }
        public int IntegrationTypeId { get; set; }

        public string? DisplayName { get; set; }
        public string? ExternalVenueId { get; set; }
        public int? LoyaltyProgramId { get; set; }

        public string? ApiBaseUrl { get; set; }
        public string? ApiTokenEncrypted { get; set; }

        public string? WalletTokenHash { get; set; }
        public string? WalletTokenEncrypted { get; set; }

        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }

        public Tenant Tenant { get; set; } = null!;
        public IntegrationType IntegrationType { get; set; } = null!;
        public LoyaltyProgram? LoyaltyProgram { get; set; }
    }
}

using Loyalty.DataAccess.Abstractions;
using Loyalty.DataAccess.Entities.Base;
using Loyalty.DataAccess.Enums;

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
        public string? ExternalProgramId { get; set; }
        public string? ExternalTerminalGroupId { get; set; }
        public int? LoyaltyProgramId { get; set; }

        public string? ApiBaseUrl { get; set; }
        public string? ApiUsername { get; set; }
        public string? ApiTokenEncrypted { get; set; }

        // iiko v2 app-auth (optional): appId gizli deyil (plain), clientSecret şifrəli saxlanılır.
        public string? ApiAppId { get; set; }
        public string? ApiSecretEncrypted { get; set; }

        public string? WalletTokenHash { get; set; }
        public string? WalletTokenEncrypted { get; set; }

        /// <summary>iiko-ya göndərilən əsas kart track-i (tam barkod / WalveroId / son N rəqəm). Tam barkod həmişə əlavə olunur.</summary>
        public PosCardTrackMode CardTrackMode { get; set; } = PosCardTrackMode.FullBarcode;
        /// <summary>LastDigits rejimində rəqəm sayı (4–8).</summary>
        public int? CardTrackDigits { get; set; }

        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }

        public Tenant Tenant { get; set; } = null!;
        public IntegrationType IntegrationType { get; set; } = null!;
        public LoyaltyProgram? LoyaltyProgram { get; set; }
    }
}

using Loyalty.DataAccess.Entities.Main;

namespace Loyalty.Business.Models.TenantIntegration
{
    /// <summary>
    /// Result of resolving a wallet webhook request (by Bearer token or venue_id + source).
    /// Does not contain credentials.
    /// </summary>
    public class WalletWebhookResolvedContext
    {
        public int TenantId { get; set; }
        public int? LoyaltyProgramId { get; set; }
        public string Source { get; set; } = string.Empty;
        public int TenantIntegrationId { get; set; }
        public string? ExternalVenueId { get; set; }

        // Multi-org (per-branch): resolved via TenantIntegrationVenue. Null for legacy single-org tenants.
        public int? BranchId { get; set; }
        public int? TenantIntegrationVenueId { get; set; }

        // iiko kart track rejimi (TenantIntegration.CardTrackMode) — LastDigits-də webhook Customer.CardNumber qısa kod ola bilər
        public Loyalty.DataAccess.Enums.PosCardTrackMode CardTrackMode { get; set; } = Loyalty.DataAccess.Enums.PosCardTrackMode.FullBarcode;
        public int? CardTrackDigits { get; set; }
    }
}

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
    }
}

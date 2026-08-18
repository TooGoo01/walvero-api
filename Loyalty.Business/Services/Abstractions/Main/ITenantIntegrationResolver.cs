using Loyalty.Business.Models.TenantIntegration;
using Loyalty.DataAccess.Entities.Main;

namespace Loyalty.Business.Services.Abstractions.Main
{
    /// <summary>
    /// Resolves wallet webhook requests to tenant and integration context.
    /// Used by Bearer token (from Authorization header) or venue_id + source from request body.
    /// </summary>
    public interface ITenantIntegrationResolver
    {
        /// <summary>Resolves by Bearer token (validates hash against WalletTokenHash).</summary>
        Task<WalletWebhookResolvedContext?> ResolveByWalletTokenAsync(string bearerToken, CancellationToken ct = default);

        /// <summary>Resolves by venue_id and source from Monyo request body.</summary>
        Task<WalletWebhookResolvedContext?> ResolveByVenueAndSourceAsync(string venueId, string source, CancellationToken ct = default);

        /// <summary>Resolves by iiko TerminalsGroup.Id and source from OrderClosed webhook body.</summary>
        Task<WalletWebhookResolvedContext?> ResolveByTerminalGroupIdAsync(string terminalGroupId, string source, CancellationToken ct = default);

        /// <summary>Gets full TenantIntegration (including for decrypting ApiToken) by id.</summary>
        Task<TenantIntegration?> GetByIdAsync(int tenantIntegrationId, CancellationToken ct = default);
    }
}

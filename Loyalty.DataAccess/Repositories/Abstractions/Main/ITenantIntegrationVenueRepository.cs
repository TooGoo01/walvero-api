using Loyalty.DataAccess.Entities.Main;

namespace Loyalty.DataAccess.Repositories.Abstractions.Main
{
    public interface ITenantIntegrationVenueRepository : IGenericRepository<TenantIntegrationVenue>
    {
        /// <summary>Resolve the branch venue by iiko TerminalsGroup.Id + source. Includes parent integration (creds/type/tenant/program).</summary>
        Task<TenantIntegrationVenue?> GetByTerminalGroupIdAndSourceAsync(string terminalGroupId, string source, CancellationToken ct = default);

        /// <summary>Resolve the branch venue by iiko organizationId (ExternalVenueId) + source. Includes parent integration.</summary>
        Task<TenantIntegrationVenue?> GetByExternalVenueIdAndSourceAsync(string externalVenueId, string source, CancellationToken ct = default);

        /// <summary>All active venues for an integration (with Branch loaded).</summary>
        Task<List<TenantIntegrationVenue>> GetActiveByIntegrationAsync(int tenantIntegrationId, CancellationToken ct = default);
    }
}

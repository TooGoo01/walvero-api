using Loyalty.DataAccess.Abstractions;
using Loyalty.DataAccess.Entities.Base;

namespace Loyalty.DataAccess.Entities.Main
{
    /// <summary>
    /// Per-branch iiko organization mapping under a TenantIntegration.
    /// Credentials live on the parent TenantIntegration (shared across orgs);
    /// each venue binds one iiko organizationId + terminalGroupId to a Branch.
    /// </summary>
    public class TenantIntegrationVenue : EntityBase, IEntity
    {
        public int TenantIntegrationId { get; set; }
        public int? BranchId { get; set; }

        public string ExternalVenueId { get; set; } = string.Empty;   // iiko organizationId (GUID)
        public string? ExternalTerminalGroupId { get; set; }          // iiko TerminalsGroup.Id
        public string? ExternalProgramId { get; set; }                // per-org loyalty program (fallback: parent)
        public string? DisplayName { get; set; }

        public TenantIntegration TenantIntegration { get; set; } = null!;
        public Branch? Branch { get; set; }
    }
}

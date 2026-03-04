using Loyalty.DataAccess.Abstractions;
using Loyalty.DataAccess.Entities.Base;

namespace Loyalty.DataAccess.Entities.Main
{
    /// <summary>
    /// Reference table for available POS/kassa integration types (e.g. Monyo POS).
    /// Source is the unique identifier used in webhooks and API (e.g. "monyo_pos").
    /// </summary>
    public class IntegrationType : EntityBase, IEntity
    {
        public string Name { get; set; } = string.Empty;
        public string Source { get; set; } = string.Empty;
        public string? DefaultBaseUrl { get; set; }
        public string? Description { get; set; }

        public ICollection<TenantIntegration> TenantIntegrations { get; set; } = new List<TenantIntegration>();
    }
}

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
        public bool IsEnrolledInProgram { get; set; }

        // Multi-org (per-branch): hansı iiko venue/org-da yaradılıb. Legacy tək-org sətirlərdə null.
        public int? TenantIntegrationVenueId { get; set; }

        // Telefonla mövcud iiko qonağına bağlananda: tapılma anındakı iiko cüzdan balansı (Walvero-ya əlavə olunacaq).
        public decimal? LegacyBalance { get; set; }
        public string? LegacyWalletId { get; set; }
        /// <summary>LegacyBalance Walvero kartına yazılıb. null + LegacyBalance&gt;0 = import gözləyir (balans iiko-ya push olunmur).</summary>
        public DateTime? LegacyImportedAt { get; set; }

        public TenantIntegration TenantIntegration { get; set; } = null!;
        public Customer Customer { get; set; } = null!;
        public TenantIntegrationVenue? TenantIntegrationVenue { get; set; }
    }
}

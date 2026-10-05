using Loyalty.DataAccess.Abstractions;
using Loyalty.DataAccess.Entities.Base;

namespace Loyalty.DataAccess.Entities.Main
{
    /// <summary>
    /// POS sifarişlərində görünmüş məhsul (iiko plugin Items[]). iiko Cloud nomenklaturası boş olan restoranlarda
    /// (delivery menyusu yoxdur) super admin bonus istisnasını bu siyahıdan seçir.
    /// </summary>
    public class PosObservedProduct : EntityBase, IEntity
    {
        public int TenantIntegrationId { get; set; }
        /// <summary>iiko məhsul GUID-i; yoxdursa "code:{ProductNumber}".</summary>
        public string ProductKey { get; set; } = string.Empty;
        public string? ProductId { get; set; }
        public string? ProductNumber { get; set; }
        public string? Name { get; set; }
        public string? CategoryId { get; set; }
        public string? CategoryName { get; set; }
        public DateTime FirstSeenAtUtc { get; set; }
        public DateTime LastSeenAtUtc { get; set; }

        public TenantIntegration TenantIntegration { get; set; } = null!;
    }
}

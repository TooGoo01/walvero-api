using Loyalty.DataAccess.Abstractions;
using Loyalty.DataAccess.Entities.Base;

namespace Loyalty.DataAccess.Entities.Main
{
    /// <summary>
    /// Maps POS product IDs to steps/points earned per unit when customer buys that product.
    /// Used by collect (order closed) to add loyalty stamps/points.
    /// </summary>
    public class PosProductEarnRule : EntityBase, IEntity
    {
        public int TenantIntegrationId { get; set; }

        /// <summary>Product (default) / Group (alt-qruplar daxil) / Category — ExternalProductId bu növün ID-sidir.</summary>
        public PosEarnExclusionKind Kind { get; set; } = PosEarnExclusionKind.Product;

        /// <summary>Product: iiko GUID və ya kod; Group/Category: iiko qrup/kateqoriya GUID-i.</summary>
        public string ExternalProductId { get; set; } = string.Empty;

        /// <summary>Məhsul kodu (plugin ProductNumber) — yalnız Product, GUID ilə yanaşı uyğunlaşma üçün.</summary>
        public string? Code { get; set; }
        public string? ProductName { get; set; }
        public int StepsPerUnit { get; set; }
        public int PointsPerUnit { get; set; }

        // null = inteqrasiyanın bütün proqramları; dolu = yalnız həmin proqramın kartları (məs. ambassador).
        // Eyni məhsul üçün proqram-spesifik qayda ümumi qaydadan üstündür.
        public int? LoyaltyProgramId { get; set; }

        public TenantIntegration TenantIntegration { get; set; } = null!;
        public LoyaltyProgram? LoyaltyProgram { get; set; }
    }
}

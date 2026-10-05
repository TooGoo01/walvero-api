using Loyalty.DataAccess.Abstractions;
using Loyalty.DataAccess.Entities.Base;

namespace Loyalty.DataAccess.Entities.Main
{
    /// <summary>
    /// iiko nomenklaturasının (məhsul / qrup / kateqoriya) yığcam surəti — org başına bir sətir.
    /// Bonus istisnalarını sifariş anında xarici çağırışsız həll etmək üçün (PosEarnExclusionService).
    /// </summary>
    public class PosNomenclatureCache : EntityBase, IEntity
    {
        public int TenantIntegrationId { get; set; }
        /// <summary>iiko organizationId (GUID).</summary>
        public string OrganizationId { get; set; } = string.Empty;
        /// <summary>[{id, code, name, parentGroup, categoryId, type}]</summary>
        public string ProductsJson { get; set; } = "[]";
        /// <summary>[{id, name, parentGroup}]</summary>
        public string GroupsJson { get; set; } = "[]";
        /// <summary>[{id, name}]</summary>
        public string CategoriesJson { get; set; } = "[]";
        public long Revision { get; set; }
        public DateTime FetchedAtUtc { get; set; }

        public TenantIntegration TenantIntegration { get; set; } = null!;
    }
}

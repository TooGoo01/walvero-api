using Loyalty.DataAccess.Abstractions;
using Loyalty.DataAccess.Entities.Base;
using Loyalty.DataAccess.Enums;

namespace Loyalty.DataAccess.Entities.Main
{
    public class MenuItemVariant : EntityBase, IEntity
    {
        public int MenuItemId { get; set; }
        public MenuItem MenuItem { get; set; } = null!;

        public VariantType VariantType { get; set; }
        public string Code { get; set; } = string.Empty; // e.g. "Regular", "S", "M", "L"

        public string? AzName { get; set; }
        public string? EnName { get; set; }
        public string? RuName { get; set; }

        public int SortOrder { get; set; }
        public bool IsDefault { get; set; }

        public double Price { get; set; }
        public double DiscountPrice { get; set; }
    }
}

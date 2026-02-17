using Loyalty.DataAccess.Abstractions;
using Loyalty.DataAccess.Entities.Base;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Loyalty.DataAccess.Entities.Main
{
    public class MenuItem : EntityBase, IEntity
    {
        public string Currency { get; set; }
        public string AzName { get; set; }
        public string EnName { get; set; }
        public string RuName { get; set; }
        public string? AzDescription { get; set; }
        public string? RuDescription { get; set; }
        public string? EnDescription { get; set; }
        public string? PrepTimeMinutes { get; set; }
        public string CurrencySign { get; set; }

        /// <summary>
        /// [DEPRECATED] Use Variants for pricing. Kept for backward compatibility during migration.
        /// All pricing should come from MenuItemVariant.
        /// </summary>
        public double Price { get; set; }

        /// <summary>
        /// [DEPRECATED] Use Variants for pricing. Kept for backward compatibility during migration.
        /// </summary>
        public double DiscountPrice { get; set; }

        public int MenuCategoryId { get; set; }
        public MenuCategory MenuCategory { get; set; }
        public ICollection<MenuItemImage> MenuItemImages { get; set; } = new List<MenuItemImage>();
        public ICollection<MenuItemVariant> Variants { get; set; } = new List<MenuItemVariant>();
    }
}

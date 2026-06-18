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
        public double Price { get; set; }
        public double DiscountPrice { get; set; }
        
        public int MenuCategoryId { get; set; }
        public MenuCategory MenuCategory { get; set; }

        public int? TenantId { get; set; }
        public Tenant? Tenant { get; set; }

        public int? BranchId { get; set; }
        public Branch? Branch { get; set; }

        // Admin-controlled display order within the category. Default 0 → falls back to Id ordering.
        public int SortOrder { get; set; }

        public string? IngredientVideoUrl { get; set; }
        public string? IngredientVideoFileName { get; set; }
        public string? IngredientVideoPosterUrl { get; set; }
        public string? IngredientVideoPosterFileName { get; set; }

        public ICollection<MenuItemImage> MenuItemImages { get; set; } = new List<MenuItemImage>();
    }
}

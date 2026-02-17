namespace Loyalty.Business.Dtos.Get.Main
{
    public class GetMenuItemDto
    {
        public int Id { get; set; }
        public string Currency { get; set; } = string.Empty;
        public string AzName { get; set; } = string.Empty;
        public string EnName { get; set; } = string.Empty;
        public string RuName { get; set; } = string.Empty;
        public string? AzDescription { get; set; }
        public string? RuDescription { get; set; }
        public string? EnDescription { get; set; }
        public string? PrepTimeMinutes { get; set; }
        public string CurrencySign { get; set; } = string.Empty;
        /// <summary>Price from default variant. Use Variants for full pricing.</summary>
        public double Price { get; set; }
        /// <summary>Discount price from default variant. Use Variants for full pricing.</summary>
        public double DiscountPrice { get; set; }
        public int MenuCategoryId { get; set; }
        public GetMenuCategoryDto? MenuCategory { get; set; }
        public List<GetMenuItemImageDto>? MenuItemImages { get; set; }
        public List<GetMenuItemVariantDto> Variants { get; set; } = new();
        /// <summary>ID of the default variant. UI hides selector when only one variant.</summary>
        public int? DefaultVariantId { get; set; }
    }
}

namespace Loyalty.Business.Dtos.Get.Main
{
    public class GetMenuItemDto
    {
        public int Id { get; set; }
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
        public GetMenuCategorySummaryDto? MenuCategory { get; set; }
        public int? TenantId { get; set; }
        public int? BranchId { get; set; }
        public string? BranchName { get; set; }
        public string? IngredientVideoUrl { get; set; }
        public string? IngredientVideoPosterUrl { get; set; }
        public List<GetMenuItemImageDto>? MenuItemImages { get; set; }
    }
}

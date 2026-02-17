namespace Loyalty.Business.Dtos.Get.Main
{
    public class GetMenuItemVariantDto
    {
        public int Id { get; set; }
        public int MenuItemId { get; set; }
        public int VariantType { get; set; }  // 1=Size
        public string Code { get; set; } = string.Empty;
        public string? AzName { get; set; }
        public string? EnName { get; set; }
        public string? RuName { get; set; }
        public int SortOrder { get; set; }
        public bool IsDefault { get; set; }
        public double Price { get; set; }
        public double DiscountPrice { get; set; }
    }
}

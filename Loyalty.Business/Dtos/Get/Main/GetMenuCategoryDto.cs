namespace Loyalty.Business.Dtos.Get.Main
{
    public class GetMenuCategoryDto
    {
        public int Id { get; set; }
        public string AzName { get; set; }
        public string EnName { get; set; }
        public string RuName { get; set; }
        public string? AzDescription { get; set; }
        public string? EnDescription { get; set; }
        public string? RuDescription { get; set; }
        public string? DescriptionColor { get; set; }
        public int? DescriptionFontSize { get; set; }
        public int TenantId { get; set; }
        public int? BranchId { get; set; }
        public string? BranchName { get; set; }
        public List<GetMenuItemDto>? MenuItems { get; set; }
    }
}

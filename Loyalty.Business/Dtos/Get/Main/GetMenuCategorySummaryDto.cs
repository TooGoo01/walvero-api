namespace Loyalty.Business.Dtos.Get.Main
{
    /// <summary>
    /// Menu category without MenuItems - used when nested inside GetMenuItemDto to avoid JSON cycle.
    /// </summary>
    public class GetMenuCategorySummaryDto
    {
        public int Id { get; set; }
        public string AzName { get; set; }
        public string EnName { get; set; }
        public string RuName { get; set; }
        public string? AzDescription { get; set; }
        public string? EnDescription { get; set; }
        public string? RuDescription { get; set; }
        public int TenantId { get; set; }
    }
}

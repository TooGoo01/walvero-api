namespace Loyalty.Business.Dtos.Get.Main
{
    public class GetTenantLinkDto
    {
        public int Id { get; set; }
        public int TenantId { get; set; }
        public int? BranchId { get; set; }
        public string? BranchName { get; set; }
        public string AzTitle { get; set; } = string.Empty;
        public string? EnTitle { get; set; }
        public string? RuTitle { get; set; }
        public string Url { get; set; } = string.Empty;
        public string? IconKey { get; set; }
        public int SortOrder { get; set; }
        public bool OpenInNewTab { get; set; }
        public bool IsActive { get; set; }
    }
}

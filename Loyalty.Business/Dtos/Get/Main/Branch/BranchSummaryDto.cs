namespace Loyalty.Business.Dtos.Get.Main.Branch
{
    public class BranchSummaryDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Address { get; set; }
        public decimal? Latitude { get; set; }
        public decimal? Longitude { get; set; }
        public string? PhotoUrl { get; set; }
        public string? LogoUrl { get; set; }
        public bool IsMainBranch { get; set; }
    }
}

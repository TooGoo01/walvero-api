namespace Loyalty.Business.Dtos.Get.Main
{
    /// <summary>
    /// Projection DTO for tenants displayed on the home page.
    /// Used for optimized read-only queries without loading full entities.
    /// </summary>
    public class HomeTenantDto
    {
        public int TenantId { get; set; }
        public string TenantName { get; set; } = string.Empty;
        public string TenantSlug { get; set; } = string.Empty;
        public string? TenantProgramName { get; set; }
        public string? GoogleLogoUrl { get; set; }
        public string? AppLogoUrl { get; set; }
        public string? AppStripUrl { get; set; }
        public string? RewardDescription { get; set; }
    }
}


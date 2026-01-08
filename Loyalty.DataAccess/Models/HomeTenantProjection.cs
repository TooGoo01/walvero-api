namespace Loyalty.DataAccess.Models
{
    /// <summary>
    /// Projection model for home page tenants. Used for optimized EF Core queries.
    /// </summary>
    public class HomeTenantProjection
    {
        public int TenantId { get; set; }
        public string TenantName { get; set; } = string.Empty;
        public string? TenantProgramName { get; set; }
        public string? GoogleLogoUrl { get; set; }
        public string? RewardDescription { get; set; }
    }
}


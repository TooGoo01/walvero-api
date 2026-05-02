namespace Loyalty.DataAccess.Models
{
    /// <summary>
    /// Projection model for home page stories. Used for optimized EF Core queries.
    /// </summary>
    public class HomeStoryProjection
    {
        public int Id { get; set; }
        public string? AzTitle { get; set; }
        public string? EnTitle { get; set; }
        public string? RuTitle { get; set; }
        public string? ImageUrl { get; set; }
        public string? SmallImageUrl { get; set; }
        public string? VideoUrl { get; set; }
        public int? TenantId { get; set; }
    }
}


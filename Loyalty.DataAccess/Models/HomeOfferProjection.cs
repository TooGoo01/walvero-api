namespace Loyalty.DataAccess.Models
{
    /// <summary>
    /// Projection model for home page offers. Used for optimized EF Core queries.
    /// </summary>
    public class HomeOfferProjection
    {
        public int Id { get; set; }
        public string? AzTitle { get; set; }
        public string? EnTitle { get; set; }
        public string? RuTitle { get; set; }
        public string? AzDescription { get; set; }
        public string? EnDescription { get; set; }
        public string? RuDescription { get; set; }
        public string? PhotoUrl { get; set; }
        public int? TenantId { get; set; }
    }
}


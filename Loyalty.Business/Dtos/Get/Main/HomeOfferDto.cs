namespace Loyalty.Business.Dtos.Get.Main
{
    /// <summary>
    /// DTO for offers displayed on the home page.
    /// Minimal fields for optimized read-only queries.
    /// </summary>
    public class HomeOfferDto
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
        public int? DiscountPercent { get; set; }
        public System.DateTime? ValidFrom { get; set; }
        public System.DateTime? ValidTo { get; set; }
        public string? AzTerms { get; set; }
        public string? EnTerms { get; set; }
        public string? RuTerms { get; set; }
    }
}


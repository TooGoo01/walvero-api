namespace Loyalty.Business.Dtos.Get.Main
{
    /// <summary>
    /// DTO for stories displayed on the home page.
    /// Minimal fields for optimized read-only queries.
    /// </summary>
    public class HomeStoryDto
    {
        public int Id { get; set; }
        public string? AzTitle { get; set; }
        public string? EnTitle { get; set; }
        public string? RuTitle { get; set; }
        public string? ImageUrl { get; set; }
        public string? SmallImageUrl { get; set; }
        public string? VideoUrl { get; set; }
        public string? VideoPosterUrl { get; set; }
        public string? MediaType { get; set; } // "video" və ya "image" — frontend isVideo yoxlaması üçün
        public int? TenantId { get; set; }
    }
}


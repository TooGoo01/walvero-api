using System;

namespace Loyalty.Business.Dtos.Get.Main.Branch
{
    public class GetBranchDto
    {
        public int Id { get; set; }
        public int TenantId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Slug { get; set; }
        public string? Address { get; set; }
        public decimal? Latitude { get; set; }
        public decimal? Longitude { get; set; }
        public int RadiusMeters { get; set; }
        public string? Phone { get; set; }
        public string? Email { get; set; }
        public string? PhotoUrl { get; set; }
        public string? LogoUrl { get; set; }
        public string? WorkingHoursJson { get; set; }
        public bool IsMainBranch { get; set; }
        public bool IsActive { get; set; }
        public DateTime RegDate { get; set; }
        public int StaffCount { get; set; }
        public double? DistanceKm { get; set; }

        public string? LocationUrl { get; set; }
        public string? WazeLocationUrl { get; set; }
        public string? MenuUrl { get; set; }
        public string? InstagramUrl { get; set; }
        public string? FacebookUrl { get; set; }
        public string? WhatsAppUrl { get; set; }
        public string? TelegramUrl { get; set; }
        public string? LinkedInUrl { get; set; }
        public string? TiktokUrl { get; set; }
        public string? TripAdvisorUrl { get; set; }
        public string? WebsiteUrl { get; set; }
        public string? YoutubeUrl { get; set; }
        public string? TwitterUrl { get; set; }
        public string? YelpUrl { get; set; }
        public string? ThreadsUrl { get; set; }
        public string? PinterestUrl { get; set; }

        public string? BackgroundColor { get; set; }
        public string? ForegroundColor { get; set; }

        // Fraud-prevention config
        public bool RequireReceiptOcr { get; set; }
        public decimal? OcrToleranceAzn { get; set; }
        public decimal? SuspiciousAmountCap { get; set; }
        public int? SuspiciousSpendCountCap { get; set; }
        public bool EnableSuspiciousAlert { get; set; }
        public string? AlertWhatsappPhone { get; set; }
    }
}

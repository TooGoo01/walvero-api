using System.ComponentModel.DataAnnotations;

namespace Loyalty.Business.Dtos.Post.Main.Branch
{
    public class CreateBranchDto
    {
        [Required]
        [MaxLength(200)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(64)]
        public string? Slug { get; set; }

        [MaxLength(500)]
        public string? Address { get; set; }

        public decimal? Latitude { get; set; }
        public decimal? Longitude { get; set; }
        public int? RadiusMeters { get; set; }

        [MaxLength(50)]
        public string? Phone { get; set; }

        [MaxLength(200)]
        public string? Email { get; set; }

        [MaxLength(500)]
        public string? PhotoUrl { get; set; }

        [MaxLength(500)]
        public string? LogoUrl { get; set; }

        public string? WorkingHoursJson { get; set; }
        public bool? IsMainBranch { get; set; }

        [MaxLength(500)] public string? LocationUrl { get; set; }
        [MaxLength(500)] public string? WazeLocationUrl { get; set; }
        [MaxLength(500)] public string? MenuUrl { get; set; }
        [MaxLength(500)] public string? InstagramUrl { get; set; }
        [MaxLength(500)] public string? FacebookUrl { get; set; }
        [MaxLength(500)] public string? WhatsAppUrl { get; set; }
        [MaxLength(500)] public string? TelegramUrl { get; set; }
        [MaxLength(500)] public string? LinkedInUrl { get; set; }
        [MaxLength(500)] public string? TiktokUrl { get; set; }
        [MaxLength(500)] public string? TripAdvisorUrl { get; set; }
        [MaxLength(500)] public string? WebsiteUrl { get; set; }
        [MaxLength(500)] public string? YoutubeUrl { get; set; }
        [MaxLength(500)] public string? TwitterUrl { get; set; }
        [MaxLength(500)] public string? YelpUrl { get; set; }
        [MaxLength(500)] public string? ThreadsUrl { get; set; }
        [MaxLength(500)] public string? PinterestUrl { get; set; }

        [MaxLength(20)] public string? BackgroundColor { get; set; }
        [MaxLength(20)] public string? ForegroundColor { get; set; }

        // --- Receipt OCR + suspicious-tx config (branch override; tenant fallback) ---
        public bool? RequireReceiptOcr { get; set; }
        public decimal? OcrToleranceAzn { get; set; }
        public decimal? SuspiciousAmountCap { get; set; }
        public int? SuspiciousSpendCountCap { get; set; }
        public bool? EnableSuspiciousAlert { get; set; }
        [MaxLength(20)] public string? AlertWhatsappPhone { get; set; }

        // --- QR + dynamic-label config (branch override; null = inherit tenant) ---
        [MaxLength(32)] public string? ObjectCode { get; set; }
        [MaxLength(300)] public string? OcrLabelAmount { get; set; }
        [MaxLength(300)] public string? OcrLabelObjectCode { get; set; }
        [MaxLength(300)] public string? OcrLabelReceiptId { get; set; }
        [MaxLength(300)] public string? OcrLabelTaxNumber { get; set; }
        [MaxLength(50)] public string? ExpectedTaxNumber { get; set; }
        public bool? RequireQrVerification { get; set; }
        public bool? NotifyUserOnMissingQr { get; set; }
        [MaxLength(100)] public string? ReceiptTimeZoneId { get; set; }

        // null → tenant.RequireConfirmation-a inherit
        public bool? RequireOtpConfirm { get; set; }
    }
}

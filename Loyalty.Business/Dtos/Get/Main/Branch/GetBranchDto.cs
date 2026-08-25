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
        public bool HideInApp { get; set; }
        public string? AppDisplayName { get; set; }
        public string? Keywords { get; set; } // AI köməkçi axtarış açar sözləri
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
        public string? WoltUrl { get; set; }
        public string? BoltFoodUrl { get; set; }

        public string? BackgroundColor { get; set; }
        public string? ForegroundColor { get; set; }

        // Fraud-prevention config
        public bool RequireReceiptOcr { get; set; }
        public decimal? OcrToleranceAzn { get; set; }
        public decimal? SuspiciousAmountCap { get; set; }
        public int? SuspiciousSpendCountCap { get; set; }
        public bool EnableSuspiciousAlert { get; set; }
        public string? AlertWhatsappPhone { get; set; }

        // QR + dynamic-label config (branch override; nullable = inherit tenant)
        public string? ObjectCode { get; set; }
        public string? OcrLabelAmount { get; set; }
        public string? OcrLabelObjectCode { get; set; }
        public string? OcrLabelReceiptId { get; set; }
        public string? OcrLabelTaxNumber { get; set; }
        public string? ExpectedTaxNumber { get; set; }
        public bool? RequireQrVerification { get; set; }
        public bool? NotifyUserOnMissingQr { get; set; }
        public bool? AllowNonFiscalReceipts { get; set; }
        public string? ReceiptBranchNames { get; set; }
        public string? ReceiptTimeZoneId { get; set; }

        // null → tenant.RequireConfirmation-a inherit
        public bool? RequireOtpConfirm { get; set; }

        // Restaurant page banner + menu page announcement
        public string? BannerVideoUrl { get; set; }
        public string? BannerVideoPosterUrl { get; set; }
        public string? BannerVideoFileName { get; set; }
        public string? BannerImagesJson { get; set; }
        public string? AnnouncementAz { get; set; }
        public string? AnnouncementEn { get; set; }
        public string? AnnouncementRu { get; set; }
        public string? MetaTitle { get; set; }
        public string? MetaDescription { get; set; }

        // Görünüş
        public int? AnnouncementFontSize { get; set; }
        public string? DefaultMenuView { get; set; }
    }
}

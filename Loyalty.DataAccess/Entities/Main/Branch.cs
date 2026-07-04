using Loyalty.DataAccess.Abstractions;
using Loyalty.DataAccess.Entities.Base;
using Loyalty.DataAccess.Entities.Users;
using System.Collections.Generic;

namespace Loyalty.DataAccess.Entities.Main
{
    public class Branch : EntityBase, IEntity
    {
        public int TenantId { get; set; }
        public Tenant? Tenant { get; set; }

        public string Name { get; set; } = string.Empty;
        public string? Slug { get; set; }
        public string? Address { get; set; }
        public decimal? Latitude { get; set; }
        public decimal? Longitude { get; set; }
        public int RadiusMeters { get; set; } = 100;
        public string? Phone { get; set; }
        public string? Email { get; set; }
        public string? PhotoUrl { get; set; }
        public string? LogoUrl { get; set; }
        public string? WorkingHoursJson { get; set; }

        public bool IsMainBranch { get; set; } = false;
        public bool HideInApp { get; set; } = false; // App listinqlərindən gizlədir; slug URL, skaner, POS, tranzaksiyalar işləyir (IsActive=false-dan fərqli)

        // Optional contact / social links � same shape as Tenant
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

        // Branch-a aid brand renkleri. Boshdursa LoyaltyProgram CardTemplate-den fallback olunur.
        public string? BackgroundColor { get; set; }
        public string? ForegroundColor { get; set; }

        // --- Receipt OCR + suspicious-tx config (branch override, tenant fallback) ---
        public bool RequireReceiptOcr { get; set; } = false;
        public decimal? OcrToleranceAzn { get; set; }              // null → fallback to tenant or 1 AZN
        public decimal? SuspiciousAmountCap { get; set; }          // null → fallback to tenant
        public int? SuspiciousSpendCountCap { get; set; }          // null → fallback to tenant
        public bool EnableSuspiciousAlert { get; set; } = false;
        public string? AlertWhatsappPhone { get; set; }            // E.164; null → fallback to tenant

        // --- QR + dynamic-label config (branch override, tenant fallback) ---
        public string? ObjectCode { get; set; }                    // e-kassa obyekt kodu; null → match skip
        public string? OcrLabelAmount { get; set; }                // null → tenant
        public string? OcrLabelObjectCode { get; set; }            // null → tenant
        public string? OcrLabelReceiptId { get; set; }             // null → tenant
        public string? OcrLabelTaxNumber { get; set; }             // null → tenant
        public string? ExpectedTaxNumber { get; set; }             // null → tenant; tenant da null → match skip
        public bool? NotifyUserOnMissingQr { get; set; }           // null → tenant
        public bool? RequireQrVerification { get; set; }           // null → tenant
        public string? ReceiptTimeZoneId { get; set; }             // null → tenant

        // Pay (redeem) üçün iki-mərhələli OTP təsdiqi. null → tenant.RequireConfirmation-a inherit.
        // false → kassir kart skan etdiyi anda balans azalır, customer-ə OTP göndərilmir.
        public bool? RequireOtpConfirm { get; set; }

        // --- Restaurant page banner + menu page announcement ---
        // Banner — admin video VƏ ya image carousel. Video varsa frontend onu üstün tutur.
        public string? BannerVideoUrl { get; set; }
        public string? BannerVideoPosterUrl { get; set; }
        public string? BannerVideoFileName { get; set; }
        // List<string> serialize olunmuş JSON. Frontend parse edir.
        public string? BannerImagesJson { get; set; }

        // Menu page-də kateqoriya siyahısının üstündə 3 dildə elan zolağı.
        public string? AnnouncementAz { get; set; }
        public string? AnnouncementEn { get; set; }
        public string? AnnouncementRu { get; set; }

        // Link preview (paylaşım) üçün admin tərəfindən təyin olunan title + description.
        public string? MetaTitle { get; set; }
        public string? MetaDescription { get; set; }

        public ICollection<Room> Rooms { get; set; } = new List<Room>();

        // Inverse navigation collections — DbContext-də HasOne(...).WithMany(b => b.Staff/Transactions)
        // konfiqurasiyaları üçün lazımdır. ApplicationUser.BranchId və Transaction.BranchId nullable
        // FK-lər vasitəsilə bağlanır; bu kolleksiyalar ehtiyac olduqda Include ilə yüklənir.
        public ICollection<ApplicationUser> Staff { get; set; } = new List<ApplicationUser>();
        public ICollection<Transaction> Transactions { get; set; } = new List<Transaction>();
    }
}

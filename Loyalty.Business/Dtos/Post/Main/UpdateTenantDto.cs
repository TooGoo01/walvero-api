using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Loyalty.Business.Dtos.Post.Main
{
    public class UpdateTenantDto
    {
        public int Id { get; set; }
        public string CompanyName { get; set; } = string.Empty;
        public string Subdomain { get; set; } = string.Empty;
        public string ContactEmail { get; set; } = string.Empty;
        public string ContactPhone { get; set; } = string.Empty; 
        public string? LocationUrl { get; set; } = string.Empty;
        public string? InstagramUrl { get; set; } = string.Empty;
        public string? FacebookUrl { get; set; } = string.Empty;
        public string? WhatsAppUrl { get; set; } = string.Empty;
        public string? TelegramUrl { get; set; } = string.Empty;
        public string? LinkedInUrl { get; set; }
        public string? WazeLocationUrl { get; set; }
        public string? MenuUrl { get; set; } = string.Empty;
        public string? TiktokUrl { get; set; }
        public string? TripAdvisorUrl { get; set; }
        public string? WebsiteUrl { get; set; }
        public string? YoutubeUrl { get; set; }
        public string? TwitterUrl { get; set; }
        public string? YelpUrl { get; set; }
        public string? ThreadsUrl { get; set; }
        public string? PinterestUrl { get; set; }
        public bool ShowOnHome { get; set; } = false;
        public bool NearbyBroadcast { get; set; } = false;
        public bool AcceptAnonymous { get; set; } = false;
        public bool IsHidden { get; set; } = false;
        public bool HideInApp { get; set; } = false;
        public bool IsHasMenu { get; set; } = false;
        public decimal? Latitude { get; set; }
        public decimal? Longitude { get; set; }
        public List<int> admins { get; set; }
        public int categoryId { get; set; }
        public bool RequireConfirmation { get; set; } = true;
        public int ConfirmationTtlMinutes { get; set; } = 10;
        public int? tenantLocationId { get; set; }
        public int? countryId { get; set; }
        public int? cityId { get; set; }
        public int? timeZoneId { get; set; }

        // --- Receipt OCR + suspicious-tx config (tenant-level fallback for branches) ---
        public bool? RequireReceiptOcr { get; set; }
        public decimal? OcrToleranceAzn { get; set; }
        public decimal? SuspiciousAmountCap { get; set; }
        public int? SuspiciousSpendCountCap { get; set; }
        public bool? EnableSuspiciousAlert { get; set; }
        [MaxLength(20)] public string? AlertWhatsappPhone { get; set; }

        // --- QR + dynamic-label config (tenant baseline; nullable means "no change") ---
        [MaxLength(300)] public string? OcrLabelAmount { get; set; }
        [MaxLength(300)] public string? OcrLabelObjectCode { get; set; }
        [MaxLength(300)] public string? OcrLabelReceiptId { get; set; }
        [MaxLength(300)] public string? OcrLabelTaxNumber { get; set; }
        [MaxLength(50)] public string? ExpectedTaxNumber { get; set; }
        public bool? RequireQrVerification { get; set; }
        public bool? NotifyUserOnMissingQr { get; set; }
        public bool? AllowNonFiscalReceipts { get; set; }
        [MaxLength(300)] public string? ReceiptBranchNames { get; set; }
        [MaxLength(100)] public string? ReceiptTimeZoneId { get; set; }
        [MaxLength(500)] public string? EkassaReceiptUrlTemplate { get; set; }
    }
}

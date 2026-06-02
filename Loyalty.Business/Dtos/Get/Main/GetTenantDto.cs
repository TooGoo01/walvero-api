using Loyalty.Business.Dtos.Get.Users;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Loyalty.Business.Dtos.Get.Main
{
    public class GetTenantDto
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
        public bool IsHidden { get; set; } = false;
        public bool IsHasMenu { get; set; } = false;
        public bool AcceptAnonymous { get; set; } = false;
        public bool RequireConfirmation { get; set; } = true;
        public int ConfirmationTtlMinutes { get; set; } = 10;

        // Fraud-prevention config (tenant-level)
        public bool RequireReceiptOcr { get; set; }
        public decimal? OcrToleranceAzn { get; set; }
        public decimal? SuspiciousAmountCap { get; set; }
        public int? SuspiciousSpendCountCap { get; set; }
        public bool EnableSuspiciousAlert { get; set; }
        public string? AlertWhatsappPhone { get; set; }

        // QR + dynamic-label config (tenant baseline)
        public string OcrLabelAmount { get; set; } = "Yekun Məbləğ,Cəmi,Total,Sum";
        public string OcrLabelObjectCode { get; set; } = "Obyektin kodu,Obyekt Kodu,Object Code";
        public string OcrLabelReceiptId { get; set; } = "Fiskal İD,Fiskal ID,Fiscal ID,Çek nömrəsi";
        public string OcrLabelTaxNumber { get; set; } = "VÖEN,VOEN,Vergi nömrəsi,Tax number,TIN";
        public string? ExpectedTaxNumber { get; set; }
        public bool RequireQrVerification { get; set; }
        public bool NotifyUserOnMissingQr { get; set; }
        public string ReceiptTimeZoneId { get; set; } = "Azerbaijan Standard Time";
        public string EkassaReceiptUrlTemplate { get; set; }
            = "https://monitoring.e-kassa.gov.az/pks-monitoring/2.0.0/documents/{docId}";
        public DateTime? SubscriptionExpiryDate { get; set; }
        public DateTime? CreatedAt { get; set; }
        public List<ApplicationUserDto> Admins { get; set; }
        public List<GetLoyaltyProgramDto> LoyaltyPrograms { get; set; }
        public int? categoryId { get; set; }
        public GetCategoryIdNameDto? Category { get; set; }
        public int? tenantLocationId { get; set; }
        public GetTenantLocationDto? TenantLocation { get; set; }

        public bool? IsCustomer { get; set; } = false;
    }
}

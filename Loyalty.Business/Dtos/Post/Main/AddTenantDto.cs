using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Loyalty.Business.Dtos.Get.Main
{
    public class AddTenantDto
    {
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
        public bool IsHidden { get; set; } = false;
        public bool IsHasMenu { get; set; } = false;
        public bool AcceptAnonymous { get; set; } = false;
        public bool RequireConfirmation { get; set; } = true;
        public int ConfirmationTtlMinutes { get; set; } = 10;
        public int? categoryId { get; set; }
        public int? tenantLocationId { get; set; }
        public int? countryId { get; set; }
        public int? cityId { get; set; }
        public int? timeZoneId { get; set; }
    }
}

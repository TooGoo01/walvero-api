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
        public string? MenuUrl { get; set; } = string.Empty;
        public bool ShowOnHome { get; set; } = false;
        public int? tenantLocationId { get; set; }
        public int? countryId { get; set; }

        [Required(ErrorMessage = "Şəhər seçimi məcburidir")]
        public int? cityId { get; set; }

        public int? timeZoneId { get; set; }
    }
}

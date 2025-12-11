using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Loyalty.Business.Dtos.Get.Main
{
    public class GetTenantBrandingDto
    {
        public int Id { get; set; }
        public string? LogoUrl { get; set; }
        public string? BackgroundImageUrl { get; set; }
        public string PrimaryColor { get; set; } = "#000000";
        public string SecondaryColor { get; set; } = "#FFFFFF";
        public string? Description { get; set; }
        public string? Website { get; set; }
        public int TenantId { get; set; }

    }
}

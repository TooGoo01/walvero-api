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
        public string BackgroundColor { get; set; } = "#FFFFFF";
        public string TextColor { get; set; } = "#000000";
        public string? Description { get; set; }
        public string? Website { get; set; }
        public string? DefaultMenuView { get; set; }
        public string? CategoryDescriptionColor { get; set; }
        public int? CategoryDescriptionFontSize { get; set; }
        public string? AnnouncementAz { get; set; }
        public string? AnnouncementEn { get; set; }
        public string? AnnouncementRu { get; set; }
        public int? AnnouncementFontSize { get; set; }
        public int TenantId { get; set; }

    }
}

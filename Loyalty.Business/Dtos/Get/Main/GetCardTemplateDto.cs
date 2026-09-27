using Loyalty.Business.Dtos.Post.Main;
using Loyalty.DataAccess.Enums;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Loyalty.Business.Dtos.Get.Main
{
    public class GetCardTemplateDto
    {
        public int Id { get; set; }
        public int LoyaltyProgramId { get; set; }
        public string TemplateName { get; set; } = string.Empty;
        public CardTemplateType TemplateType { get; set; }
        public bool OneRedeem { get; set; } = false;
        public List<IconDto> Icons { get; set; } = new();
        public int TotalSteps { get; set; }
        public bool RewardAsPoints { get; set; }              // Hədiyyə yerinə xal rejimi (yalnız ProgressBased)
        public string StepLabel { get; set; } = "";
        public string RewardDescription { get; set; } = "";
        public string? FreeRewardLabel { get; set; }  // "Pulsuz içki", "Burger" və s. (nullable, default: "Pulsuz içki")

        public string? BronzeIconUrl { get; set; }
        public int? BronzeThreshold { get; set; }
        public int? BronzePercent { get; set; }
        public int? BronzePercentCash { get; set; }

        public string? SilverIconUrl { get; set; }
        public int? SilverThreshold { get; set; }
        public int? SilverPercent { get; set; }
        public int? SilverPercentCash { get; set; }

        public string? GoldIconUrl { get; set; }
        public int? GoldThreshold { get; set; }
        public int? GoldPercent { get; set; }
        public int? GoldPercentCash { get; set; }

        public string? PlatinumIconUrl { get; set; }
        public int? PlatinumThreshold { get; set; }
        public int? PlatinumPercent { get; set; }
        public int? PlatinumPercentCash { get; set; }

        public string BackgroundColor { get; set; } = "#FFFFFF";
        public string ForegroundColor { get; set; } = "#000000";
        public string LabelColor { get; set; } = "#999999";
        public string ProgressBarColor { get; set; } = "#4CAF50";
        public string BronzeColor { get; set; } = "#CD7F32";  // Bronze color
        public string SilverColor { get; set; } = "#C0C0C0";   // Silver color
        public string GoldColor { get; set; } = "#FFD700";     // Gold color
        public string PlatinumColor { get; set; } = "#E5E4E2"; // Platinum color
        public string? LogoUrl { get; set; }
        public string? GoogleLogoUrl { get; set; }

        public string? BackgroundImageUrl { get; set; }
        public string? StripImageUrl { get; set; }
        public string? HeroImageUrl { get; set; }

        // App-specific images (.webp)
        public string? AppLogoUrl { get; set; }
        public string? AppStripUrl { get; set; }

        public string Currency { get; set; } = "AZN"; // Currency code

        public IconLayout IconLayout { get; set; }
        public bool PayMentType { get; set; } = false;

        // Currency (Para birimi: AZN, Xal, Point və s.)
       
    }
}

using Loyalty.DataAccess.Enums;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Loyalty.Business.Dtos.Post.Main
{
    public class AddCardTemplateDto
    {
        public int LoyaltyProgramId { get; set; }

        // Template Info
        public string TemplateName { get; set; } = string.Empty;
        public CardTemplateType TemplateType { get; set; }

        // ===== PROGRESS BASED =====
        public List<IconDto> Icons { get; set; } = new();

        public int TotalSteps { get; set; } = 10;
        public string StepLabel { get; set; } = "Coffee";
        public string RewardDescription { get; set; } = string.Empty;
        public string? FreeRewardLabel { get; set; }  // "Pulsuz içki", "Burger" və s.

        // ===== POINTS BASED =====
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

        // Fayl yükləmələri (opsional)
        public IFormFile? BronzeIconFile { get; set; }
        public IFormFile? SilverIconFile { get; set; }
        public IFormFile? GoldIconFile { get; set; }
        public IFormFile? PlatinumIconFile { get; set; }

        // ===== DESIGN =====
        public string BackgroundColor { get; set; } = "#FFFFFF";
        public string ForegroundColor { get; set; } = "#000000";
        public string LabelColor { get; set; } = "#999999";
        public string ProgressBarColor { get; set; } = "#4CAF50";
        public string BronzeColor { get; set; } = "#CD7F32";  // Bronze color
        public string SilverColor { get; set; } = "#C0C0C0";   // Silver color
        public string GoldColor { get; set; } = "#FFD700";     // Gold color
        public string PlatinumColor { get; set; } = "#E5E4E2"; // Platinum color
        // Images (URL və ya File)
        public string? LogoUrl { get; set; }
        public IFormFile? LogoFile { get; set; }

        public string? GoogleLogoUrl { get; set; }
        public IFormFile? GoogleLogoFile { get; set; }

        public string? BackgroundImageUrl { get; set; }
        public IFormFile? BackgroundImageFile { get; set; }

        public string? StripImageUrl { get; set; }      // Apple Wallet banner
        public IFormFile? StripImageFile { get; set; }

        public string? HeroImageUrl { get; set; }       // Google Wallet hero
        public IFormFile? HeroImageFile { get; set; }

        // App-specific images (.webp)
        public string? AppLogoUrl { get; set; }
        public IFormFile? AppLogoFile { get; set; }
        public string? AppStripUrl { get; set; }
        public IFormFile? AppStripFile { get; set; }

        // Layout
        public IconLayout IconLayout { get; set; } = IconLayout.Grid;

        // Currency (Para birimi: AZN, Xal, Point və s.)
        public string Currency { get; set; } = "AZN"; // Default: AZN

        public bool PayMentType { get; set; } = false;
    }
}

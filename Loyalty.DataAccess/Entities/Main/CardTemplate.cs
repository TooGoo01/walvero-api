using Loyalty.DataAccess.Abstractions;
using Loyalty.DataAccess.Entities.Base;
using Loyalty.DataAccess.Enums;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Loyalty.DataAccess.Entities.Main
{
    public class CardTemplate:EntityBase,IEntity
    {
        public int LoyaltyProgramId { get; set; }

        // Template Info
        public string TemplateName { get; set; } = string.Empty;
        public CardTemplateType TemplateType { get; set; }

        // ===== PROGRESS BASED (Coffee Card style) =====

        public ICollection<Icon> Icons { get; set; }
        public bool OneRedeem { get; set; } = false;

        // Progress Settings
        public int TotalSteps { get; set; } = 10;                     // məs: 10 qəhvə
        public string StepLabel { get; set; } = "Coffee";              // "Coffee", "Visit", "Purchase"
        public string RewardDescription { get; set; } = string.Empty;  // "Get 1 FREE coffee"
        public string? FreeRewardLabel { get; set; }                   // "Pulsuz içki", "Burger" və s. (nullable, default: "Pulsuz içki")

        // ===== POINTS BASED (Traditional loyalty) =====

        // Dynamic Icons by Points
        public string? BronzeIconUrl { get; set; }
        public int? BronzeThreshold { get; set; }
        public int? BronzePercent { get; set; }
        public int? BronzePercentCash { get; set; }
        public string BronzeColor { get; set; } = "#CD7F32";  // Bronze color
        public string SilverColor { get; set; } = "#C0C0C0";   // Silver color
        public string GoldColor { get; set; } = "#FFD700";     // Gold color
        public string PlatinumColor { get; set; } = "#E5E4E2"; // Platinum color

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
      
        public bool PayMentType { get; set; } = false;
        // ===== WALLET CARD DESIGN =====

        // Colors
        public string BackgroundColor { get; set; } = "#FFFFFF";
        public string ForegroundColor { get; set; } = "#000000";
        public string LabelColor { get; set; } = "#999999";
        public string ProgressBarColor { get; set; } = "#4CAF50";

        // Images
        public string? GoogleLogoUrl { get; set; }
        public string? LogoUrl { get; set; }
        public string? BackgroundImageUrl { get; set; }
        public string? StripImageUrl { get; set; }       // Apple Wallet banner
        public string? HeroImageUrl { get; set; }        // Google Wallet hero

        // App-specific images (.webp) — wallet PNG-lərinə toxunulmur
        public string? AppLogoUrl { get; set; }
        public string? AppStripUrl { get; set; }

        public string Currency { get; set; } = "AZN"; // Default currency

        // Layout
        public IconLayout IconLayout { get; set; } = IconLayout.Grid; // Grid, Horizontal, Vertical

        // Currency (Para birimi: AZN, Xal, Point və s.)
     

        // Navigation Property
        public LoyaltyProgram LoyaltyProgram { get; set; } = null!;

    }
}

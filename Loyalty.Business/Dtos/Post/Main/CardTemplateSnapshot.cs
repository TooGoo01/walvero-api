using Loyalty.DataAccess.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Loyalty.Business.Dtos.Post.Main
{
    // Business/Dtos/Runtime/CardTemplateSnapshot.cs
    public sealed class CardTemplateSnapshot
    {
        // ---- Program meta ----
        public int LoyaltyProgramId { get; set; }
        public int TenantId { get; set; }
        public string ProgramName { get; set; } = "Loyalty";
        public string? ProgramDescription { get; set; }
        public string ProgramCode { get; set; } = string.Empty; // LP.Code

        // ---- Template meta ----
        public string TemplateName { get; set; } = string.Empty;
        public CardTemplateType TemplateType { get; set; } = CardTemplateType.ProgressBased;
        public IconLayout IconLayout { get; set; } = IconLayout.Grid;

        // ---- Progress Based ----
        public int TotalSteps { get; set; } = 10;
        public string StepLabel { get; set; } = "Visit";
        public string RewardDescription { get; set; } = "Get 1 FREE";
        public string? FreeRewardLabel { get; set; }  // From CardTemplate.FreeRewardLabel

        // ---- Points Based ----
        public int StartPoints { get; set; } = 0;           // obyekt yarananda başlanğıc points
        public decimal? PointsPerCurrency { get; set; }     // LP-dən: 1 AZN = X point
        public decimal? CurrencyPerPoint { get; set; }      // LP-dən: 1 point = X AZN
        public int? MinPointsToRedeem { get; set; }         // LP-dən
        public int? PointsExpiryDays { get; set; }          // LP-dən

        // ---- Tier Based (ikonlar + hədəflər) ----
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
        public bool OneRedeem { get; set; } = false;
        public bool PayMentType { get; set; } = false;
        
        // ---- Currency ----
        public string Currency { get; set; } = "AZN";
        
        // ---- Rənglər / dizayn ----
        public string BackgroundColor { get; set; } = "#FFFFFF";
        public string ForegroundColor { get; set; } = "#000000";
        public string LabelColor { get; set; } = "#999999";
        public string ProgressBarColor { get; set; } = "#4CAF50";

        // ---- Şəkillər ----
        public string? LogoUrl { get; set; }               // brand/logo
        public string? GoogleLogoUrl { get; set; }               // brand/logo
        public string? BackgroundImageUrl { get; set; }
        public string? StripImageUrl { get; set; }         // Apple Wallet banner
        public string? HeroImageUrl { get; set; }          // Google Wallet hero

        // ---- App-specific images (.webp) ----
        public string? AppLogoUrl { get; set; }
        public string? AppStripUrl { get; set; }

        // ---- Dinamik ikon list (Progress vizual üçün) ----
        public List<string> IconUrls { get; set; } = new(); // CardTemplate.Icons -> absolute URL-lər

        // ---- Currency (Para birimi) ----
     
    }

}

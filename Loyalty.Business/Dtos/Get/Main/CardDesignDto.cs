using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Loyalty.Business.Dtos.Get.Main
{
    public class CardDesignDto
    {
        public string BackgroundColor { get; set; } = "#FFFFFF";
        public string ForegroundColor { get; set; } = "#000000";
        public string LabelColor { get; set; } = "#000000";
        public string? HeroImageUrl { get; set; }
        public string? LogoImageUrl { get; set; }
        public string? GoogleLogoUrl { get; set; }  // Same as LogoImageUrl, for convenience
        public string? StripImageUrl { get; set; }
        public string ProgramName { get; set; } = string.Empty;
        public string? TierName { get; set; }
        public string? ProgramDescription { get; set; }
        public string? RewardDescription { get; set; }
    }
    public class UserCardDto
    {
        public int TenantId { get; set; }
        public string TenantName { get; set; } = string.Empty;
        public string CustomerFullName { get; set; } = string.Empty;
        public string? LocationUrl { get; set; } = string.Empty;

        public int CardId { get; set; }
        public int? categoryId { get; set; }
        public string? categoryEn { get; set; }
        public string? categoryAz { get; set; }
        public string? categoryRu { get; set; }
        public int ProgramId { get; set; }
        public string ProgramName { get; set; } = string.Empty;
        public decimal? CashbackPercent { get; init; } // 2
        public decimal? CashbackPercentCash { get; init; } // 4
        public decimal CurrentPoints { get; set; }
        public string CardNumber { get; set; } = string.Empty;
        public string BarcodeValue { get; set; } = string.Empty;

        public CardDesignDto Design { get; set; } = new();
        
        // ProgressBased card fields
        public string? progressText { get; set; }  // e.g., "5/10"
        public string? freeRewardText { get; set; }  // e.g., "1 Pulsuz içki"
    }
}

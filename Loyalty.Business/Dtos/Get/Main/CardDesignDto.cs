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
        public string? LogoImageUrl { get; set; }

        // App-specific images (.webp)
        public string? AppLogoUrl { get; set; }
        public string? AppStripUrl { get; set; }
    }
    public class UserCardDto
    {
        public int TenantId { get; set; }
        public string TenantName { get; set; } = string.Empty;
        public string? LocationUrl { get; set; } = string.Empty;

        // Yaxınlıq sıralaması üçün (əsas/ilk filial, yoxdursa tenant koordinatı)
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
        // Son ziyarət (bu karta aid ən son transaction), UTC
        public DateTime? LastVisitedAtUtc { get; set; }

        public int CardId { get; set; }
        public int? categoryId { get; set; }
        public int ProgramId { get; set; }
        public string ProgramName { get; set; } = string.Empty;
        public string? ProgramDescription { get; set; }
        public string? RewardDescription { get; set; }
        public decimal CurrentPoints { get; set; }

        public CardDesignDto Design { get; set; } = new();
        
        // ProgressBased card fields
        public string? progressText { get; set; }  // e.g., "5/10"
        public string? freeRewardText { get; set; }  // e.g., "1 Pulsuz içki"
    }
}

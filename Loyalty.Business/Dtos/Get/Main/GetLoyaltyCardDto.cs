using Loyalty.DataAccess.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Loyalty.Business.Dtos.Get.Main
{
    public class GetLoyaltyCardDto
    {
        public int Id { get; set; }
        public int CustomerId { get; set; } // YENİ (əvvəlki UserId)
        public int TenantId { get; set; }
        public int LoyaltyProgramId { get; set; }

        // Card Details
        public string CardNumber { get; set; } = string.Empty;
        public string? BarcodeValue { get; set; }
        /// <summary>iiko kassasında əsas kart track-i (inteqrasiyanın CardTrackMode-una görə; tam barkoddan fərqlidirsə admin göstərir).</summary>
        public string? PosTrack { get; set; }

        // Points
        public decimal CurrentPoints { get; set; } = 0;
        public decimal LifetimePoints { get; set; } = 0;
        public int CurrentSteps { get; set; } = 0;        // neçə qəhvə alınıb (məs: 7/10)
        public int CompletedCycles { get; set; } = 0;
        public int AvailableRewardCount { get; set; } = 0;  // = CompletedCycles
        public int MaxSpendCount { get; set; } = 0;         // = CompletedCycles (UI input limit)
        public string? FreeRewardLabel { get; set; }         // "Pulsuz içki", "Burger" və s. (default: "Pulsuz içki")
        public string? Currency { get; set; }  // Currency from CardTemplate (e.g., "AZN", "USD", "EUR")

        /// <summary>Balansın son istifadə tarixi (UTC, ambassador). null = müddətsiz.</summary>
        public DateTime? PointsExpiresAt { get; set; }
        public bool IsPointsExpired { get; set; }

        // Tier %-ləri CardTemplate-dən kopyalanır (frontend tier-card bunları oxuyur)
        public int? BronzeThreshold { get; set; }
        public int? SilverThreshold { get; set; }
        public int? GoldThreshold { get; set; }
        public int? PlatinumThreshold { get; set; }
        public int? BronzePercent { get; set; }
        public int? SilverPercent { get; set; }
        public int? GoldPercent { get; set; }
        public int? PlatinumPercent { get; set; }
        public int? BronzePercentCash { get; set; }
        public int? SilverPercentCash { get; set; }
        public int? GoldPercentCash { get; set; }
        public int? PlatinumPercentCash { get; set; }
        public bool PayMentType { get; set; }
    }
}

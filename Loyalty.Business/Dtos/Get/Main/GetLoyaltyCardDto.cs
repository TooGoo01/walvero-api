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

        // Points
        public int CurrentPoints { get; set; } = 0;
        public int LifetimePoints { get; set; } = 0;
        public int CurrentSteps { get; set; } = 0;        // neçə qəhvə alınıb (məs: 7/10)
        public int CompletedCycles { get; set; } = 0;
        public int AvailableRewardCount { get; set; } = 0;  // = CompletedCycles
        public int MaxSpendCount { get; set; } = 0;         // = CompletedCycles (UI input limit)
        public string? FreeRewardLabel { get; set; }         // "Pulsuz içki", "Burger" və s. (default: "Pulsuz içki")

        // Status
 
    }
}

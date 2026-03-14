using Loyalty.DataAccess.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Loyalty.Business.Dtos.Post.Main
{
    public class AddLoyaltyProgramDto
    {
        public int? TenantId { get; set; }                 // boş gələrsə ITenantProvider-dən götürəcəyik
        public string ProgramName { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? Code { get; set; }                   // boşdursa ProgramName-dən generasiya
        public decimal PointsPerCurrency { get; set; } = 1; // 1 AZN = X point
        public decimal CurrencyPerPoint { get; set; } = 0.1m;
        public LoyaltyProgramType ProgramType { get; set; } = LoyaltyProgramType.ProgressBased;
        public int MinPointsToRedeem { get; set; } = 0;
        public int? PointsExpiryDays { get; set; }

        // Lottery
        public bool IsLotteryEnabled { get; set; } = false;
        public int LotteryPurchaseThreshold { get; set; } = 5;
        public decimal? LotteryAmountThreshold { get; set; }
    }
}

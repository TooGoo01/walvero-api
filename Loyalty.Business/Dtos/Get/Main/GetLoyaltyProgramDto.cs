using Loyalty.DataAccess.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Loyalty.Business.Dtos.Get.Main
{
    public class GetLoyaltyProgramDto
    {
        public int Id { get; set; }
        public int? TenantId { get; set; }                
        public string ProgramName { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? Code { get; set; }                   // boşdursa ProgramName-dən generasiya
        public decimal PointsPerCurrency { get; set; } = 1; // 1 AZN = X point
        public decimal CurrencyPerPoint { get; set; } = 0.1m;
        public LoyaltyProgramType ProgramType { get; set; } = LoyaltyProgramType.ProgressBased;
        public int MinPointsToRedeem { get; set; } = 0;
        public int? PointsExpiryDays { get; set; }

        // Lottery
        public bool IsLotteryEnabled { get; set; }
        public int LotteryPurchaseThreshold { get; set; }
        public decimal? LotteryAmountThreshold { get; set; }

        public GetCardTemplateDto? CardTemplate { get; set; }
    }
}

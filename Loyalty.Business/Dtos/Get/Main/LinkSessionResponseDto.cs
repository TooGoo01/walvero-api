using System;
using System.Collections.Generic;

namespace Loyalty.Business.Dtos.Get.Main
{
    public class LinkSessionResponseDto
    {
        public long? WalveroId { get; set; }
        public decimal LoyaltyBalance { get; set; }
        public RewardsSummary RewardsSummary { get; set; } = new RewardsSummary();
    }

    public class RewardsSummary
    {
        public int AvailableRewardCount { get; set; }
        public string? FreeRewardLabel { get; set; }
        public int CompletedCycles { get; set; }
        public int CurrentSteps { get; set; }
        public int TotalSteps { get; set; }
    }
}


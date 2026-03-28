using Loyalty.DataAccess.Abstractions;
using Loyalty.DataAccess.Entities.Base;
using Loyalty.DataAccess.Enums;

namespace Loyalty.DataAccess.Entities.Main
{
    public class LotteryDraw : EntityBase, IEntity
    {
        public int TenantId { get; set; }
        public int LoyaltyProgramId { get; set; }
        public string DrawName { get; set; } = string.Empty;
        public string? PrizeName { get; set; }
        public string? PrizeDescription { get; set; }
        public int WinnerCount { get; set; } = 1;
        public LotteryDrawStatus Status { get; set; } = LotteryDrawStatus.Pending;
        public DateTime? ScheduledAt { get; set; }
        public DateTime? DrawnAt { get; set; }

        // Navigation Properties
        public LoyaltyProgram LoyaltyProgram { get; set; } = null!;
        public ICollection<LotteryTicket> WinningTickets { get; set; } = new List<LotteryTicket>();
        public ICollection<LotteryPrize> Prizes { get; set; } = new List<LotteryPrize>();
    }
}

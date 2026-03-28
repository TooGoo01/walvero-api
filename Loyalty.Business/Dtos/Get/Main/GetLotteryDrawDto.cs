using Loyalty.DataAccess.Enums;

namespace Loyalty.Business.Dtos.Get.Main
{
    public class GetLotteryDrawDto
    {
        public int Id { get; set; }
        public string DrawName { get; set; } = string.Empty;
        public string? PrizeName { get; set; }
        public string? PrizeDescription { get; set; }
        public int WinnerCount { get; set; }
        public LotteryDrawStatus Status { get; set; }
        public DateTime? ScheduledAt { get; set; }
        public DateTime? DrawnAt { get; set; }
        public int LoyaltyProgramId { get; set; }
        public int TenantId { get; set; }
        public DateTime CreatedAt { get; set; }
        public List<GetLotteryTicketDto> Winners { get; set; } = new();
        public List<GetLotteryPrizeDto> Prizes { get; set; } = new();
        public int TotalTickets { get; set; }
    }

    public class GetLotteryPrizeDto
    {
        public int Id { get; set; }
        public int Position { get; set; }
        public string PrizeName { get; set; } = string.Empty;
        public string? PrizeDescription { get; set; }
    }
}

namespace Loyalty.Business.Dtos.Post.Main
{
    public class CreateLotteryDrawDto
    {
        public string DrawName { get; set; } = string.Empty;
        public string? PrizeName { get; set; }
        public string? PrizeDescription { get; set; }
        public int WinnerCount { get; set; } = 1;
        public int LoyaltyProgramId { get; set; }
        public DateTime? ScheduledAt { get; set; }
        public List<CreateLotteryPrizeDto>? Prizes { get; set; }
    }

    public class CreateLotteryPrizeDto
    {
        public int Position { get; set; }           // 1 = 1st place, 2 = 2nd, 3 = 3rd
        public string PrizeName { get; set; } = string.Empty;
        public string? PrizeDescription { get; set; }
    }
}

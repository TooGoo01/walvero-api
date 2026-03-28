using Loyalty.DataAccess.Abstractions;
using Loyalty.DataAccess.Entities.Base;

namespace Loyalty.DataAccess.Entities.Main
{
    public class LotteryPrize : EntityBase, IEntity
    {
        public int LotteryDrawId { get; set; }
        public int Position { get; set; }           // 1 = 1st place, 2 = 2nd place, etc.
        public string PrizeName { get; set; } = string.Empty;
        public string? PrizeDescription { get; set; }

        // Navigation
        public LotteryDraw LotteryDraw { get; set; } = null!;
    }
}

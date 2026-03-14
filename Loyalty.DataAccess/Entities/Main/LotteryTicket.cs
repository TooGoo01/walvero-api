using Loyalty.DataAccess.Abstractions;
using Loyalty.DataAccess.Entities.Base;
using Loyalty.DataAccess.Enums;

namespace Loyalty.DataAccess.Entities.Main
{
    public class LotteryTicket : EntityBase, IEntity
    {
        public int LoyaltyCardId { get; set; }
        public int TenantId { get; set; }
        public string TicketCode { get; set; } = string.Empty;
        public LotteryTicketStatus Status { get; set; } = LotteryTicketStatus.Active;
        public int? LotteryDrawId { get; set; }

        // Navigation Properties
        public LoyaltyCard LoyaltyCard { get; set; } = null!;
        public LotteryDraw? LotteryDraw { get; set; }
    }
}

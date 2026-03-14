using Loyalty.DataAccess.Enums;

namespace Loyalty.Business.Dtos.Get.Main
{
    public class GetLotteryTicketDto
    {
        public int Id { get; set; }
        public string TicketCode { get; set; } = string.Empty;
        public LotteryTicketStatus Status { get; set; }
        public int LoyaltyCardId { get; set; }
        public string? CustomerName { get; set; }
        public string? CardNumber { get; set; }
        public int? LotteryDrawId { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}

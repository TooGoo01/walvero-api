using Loyalty.DataAccess.Entities.Main;
using Loyalty.DataAccess.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Loyalty.Business.Dtos.Get.Main
{
    public class GetTransactionDto
    {
        public int Id { get; set; }
        public int LoyaltyCardId { get; set; }

        public TransactionType Type { get; set; }
        public RedeemStatus Status { get; set; }
        public int Points { get; set; }
        public decimal? Amount { get; set; }

        public string? Description { get; set; }
        public string? ReverseReason { get; set; }
        public string? ReferenceNumber { get; set; }
        public string? OrderId { get; set; }
        public DateTimeOffset LocalOccurredAt { get; set; }
        public string TimeZoneIdUsed { get; set; } = "UTC";
        public int BalanceAfter { get; set; }

        public string? CustomerName { get; set; }
        public string? CardNumber { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}

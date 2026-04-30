using Loyalty.DataAccess.Abstractions;
using Loyalty.DataAccess.Entities.Base;
using Loyalty.DataAccess.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Loyalty.DataAccess.Entities.Main
{
    public class Transaction : EntityBase,IEntity
    {
        public int LoyaltyCardId { get; set; }

        public TransactionType Type { get; set; }
        public RedeemStatus Status { get; set; } = RedeemStatus.Pending;
        public decimal Points { get; set; }
        public decimal? Amount { get; set; } 

        public string? Description { get; set; }
        public string? ReverseReason { get; set; }
        public string? ReferenceNumber { get; set; }
        public string? OrderId { get; set; }

        public decimal BalanceAfter { get; set; }
        // Timezone tracking
        public DateTimeOffset LocalOccurredAt { get; set; } // local + offset
        public string TimeZoneIdUsed { get; set; } = "UTC"; // tenant's timezone

        public int? BranchId { get; set; }
        public Branch? Branch { get; set; }

        public LoyaltyCard LoyaltyCard { get; set; } = null!;
    }
}

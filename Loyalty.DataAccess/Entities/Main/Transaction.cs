using Loyalty.DataAccess.Abstractions;
using Loyalty.DataAccess.Entities.Base;
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
        public int Points { get; set; }
        public decimal? Amount { get; set; } // əgər alış-veriş varsa

        public string? Description { get; set; }
        public string? ReferenceNumber { get; set; }
        public string? OrderId { get; set; }

        public int BalanceAfter { get; set; } // transaksiyadan sonra balans

        // Navigation Property
        public LoyaltyCard LoyaltyCard { get; set; } = null!;
    }
}

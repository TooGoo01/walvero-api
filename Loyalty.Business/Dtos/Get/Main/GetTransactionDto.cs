using Loyalty.DataAccess.Entities.Main;
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
        public int Points { get; set; }
        public decimal? Amount { get; set; } // əgər alış-veriş varsa

        public string? Description { get; set; }
        public string? ReferenceNumber { get; set; }
        public string? OrderId { get; set; }

        public int BalanceAfter { get; set; }

    }
}

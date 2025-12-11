using Loyalty.DataAccess.Entities.Main;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Loyalty.Business.Dtos.Post.Main
{
    public class AddTransactionDto
    {
        public int LoyaltyCardId { get; set; }
        public TransactionType Type { get; set; }   
        public int Points { get; set; }
        public decimal? Amount { get; set; }
        public string? Description { get; set; }
        public string? ReferenceNumber { get; set; }
    }
}

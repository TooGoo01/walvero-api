using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Loyalty.Business.Dtos.Get.Main
{
    public class ReverseTransactionResponseDto
    {
        public string Status { get; set; } = string.Empty;
        public decimal? ReversedAmount { get; set; }
        public decimal ReversedPoints { get; set; }
        public decimal UpdatedBalance { get; set; }
    }
}


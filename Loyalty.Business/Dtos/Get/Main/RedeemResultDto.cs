using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Loyalty.Business.Dtos.Get.Main
{
    public sealed class RedeemResultDto
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public bool? RequiresOtp { get; set; }

        public int? LoyaltyCardId { get; set; }
        public int? AppliedDelta { get; set; }
        public int? OldPoints { get; set; }
        public int? NewPoints { get; set; }
        public bool? IsPending { get; set; }
        public int? RedeemRequestId { get; set; }
    }
}

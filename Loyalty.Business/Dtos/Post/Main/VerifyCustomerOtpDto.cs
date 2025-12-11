using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Loyalty.Business.Dtos.Post.Main
{
    public sealed class VerifyCustomerOtpDto
    {
        public int CustomerId { get; set; }
        public int LoyaltyProgramId { get; set; }
        public string Code { get; set; } = "";
        public string? UserAgent { get; set; }
        public string? DevicePlatform { get; set; } = "";
    }
}

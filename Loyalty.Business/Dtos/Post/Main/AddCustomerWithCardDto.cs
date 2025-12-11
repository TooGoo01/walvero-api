using Loyalty.DataAccess.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Loyalty.Business.Dtos.Post.Main
{
    public sealed class AddCustomerWithCardDto
    {
        public int TenantId { get; set; }
        public int LoyaltyProgramId { get; set; }
       
        public string? DevicePlatform { get; set; } = "";
     
    }
}

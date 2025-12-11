using Loyalty.DataAccess.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Loyalty.Business.Dtos.Post.Main
{
    public class AddWalletPassDto
    {
        public int LoyaltyCardId { get; set; }
        public WalletPlatform Platform { get; set; }
    }
}

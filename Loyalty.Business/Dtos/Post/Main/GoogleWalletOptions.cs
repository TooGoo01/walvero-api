using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Loyalty.Business.Dtos.Post.Main
{
    public class GoogleWalletOptions
    {
        public string IssuerId { get; set; } = string.Empty;
        public string ClassSuffix { get; set; } = string.Empty;
        public string ServiceAccountJsonPath { get; set; } = string.Empty;
        public string ApplicationName { get; set; } = "Loyalty Wallet App";
        public string[]? Origins { get; set; }
    }
}

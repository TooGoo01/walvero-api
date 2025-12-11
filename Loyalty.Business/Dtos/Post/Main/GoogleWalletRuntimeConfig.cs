using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Loyalty.Business.Dtos.Post.Main
{
    public sealed class GoogleWalletRuntimeConfig
    {
        public string IssuerId { get; set; } = default!;
        public string ClassSuffix { get; set; } = "loyalty";
        public string ServiceAccountJsonPath { get; set; } = default!;
        public string ApplicationName { get; set; } = "Walvero Loyalty";
        public string[] Origins { get; set; } = Array.Empty<string>();
    }
}

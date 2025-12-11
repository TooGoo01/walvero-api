using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Loyalty.Business.Dtos.Post.Main
{
    public sealed class AppleWalletOptions
    {
        public string TeamIdentifier { get; set; } = "";
        public string PassTypeIdentifier { get; set; } = "";
        public string P12Path { get; set; } = "";
        public string P12Password { get; set; } = "";
        public string WwdrCertPath { get; set; } = "";
        public string? WebServiceUrl { get; set; }
        public int AuthenticationTokenLength { get; set; } = 48;
    }
}

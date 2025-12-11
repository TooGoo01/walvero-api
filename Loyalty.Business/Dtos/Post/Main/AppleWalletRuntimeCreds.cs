using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Loyalty.Business.Dtos.Post.Main
{
    public sealed class AppleWalletRuntimeCreds
    {
        public string TeamIdentifier { get; set; } = null!;
        public string PassTypeIdentifier { get; set; } = null!;
        public string CertP12Path { get; set; } = null!;
        public string CertPassword { get; set; } = null!;
        public string WwdrCertPath { get; set; } = null!;
        public string? WebServiceUrl { get; set; }   // optional (push/update üçün)
        public string? AuthToken { get; set; }       // optional (push/update üçün)
        public string OutputDir { get; set; } = "wwwroot/passes";
    }
}

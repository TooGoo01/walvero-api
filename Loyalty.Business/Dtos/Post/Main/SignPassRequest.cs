using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Loyalty.Business.Dtos.Post.Main
{
    public sealed class SignPassRequest
    {
        // Working directory: pass.json + image assets buradadır
        public string WorkingDirectory { get; set; } = null!;
        public string OutputPkPassPath { get; set; } = null!;

        public string AppleCertP12Path { get; set; } = null!;
        public string AppleCertPassword { get; set; } = null!;
        public string AppleWwdrCertPath { get; set; } = null!;
    }
}

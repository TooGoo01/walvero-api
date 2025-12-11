using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Loyalty.Business.Dtos.Post.Main
{
    public sealed class AppleWalletRuntimeConfig
    {
        // **Məcburi**
        public string PassTypeIdentifier { get; set; } = null!; // pass.com.your.bundle
        public string TeamIdentifier { get; set; } = null!;     // Apple Team ID (10 simvol)
        public string OrganizationName { get; set; } = "Walvero";

        public string SignCertP12Path { get; set; } = null!;    // .p12
        public string SignCertPassword { get; set; } = null!;   // .p12 şifrəsi

        // **Opsional**
        public string? AppleWwdrCertPath { get; set; }          // WWDR .cer (olmasa da olar)
        public string? WebServiceUrl { get; set; }              // PassKit Web Service — istəyə görə
        public string? AuthenticationToken { get; set; }        // PassKit Web Service token — istəyə görə

        // Vizual və fayl yolları (opsional, amma praktik)
        public string ForegroundColorHex { get; set; } = "#FFFFFF";
        public string BackgroundColorHex { get; set; } = "#0EA5E9";

        public string WebRootPath { get; set; } = "";           // wwwroot
        public string PublicBaseUrl { get; set; } = "";         // https://{tenant-domain}
    }

}

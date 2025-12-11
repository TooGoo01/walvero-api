using Loyalty.DataAccess.Enums;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Loyalty.Business.Dtos.Post.Main
{
    public class AddWalletCredentialDto
    {
        public int TenantId { get; set; }
        public WalletPlatform Platform { get; set; } // GooglePay=1, AppleWallet=2, ...

        // -------- APPLE --------
        public string? TeamIdentifier { get; set; }           // Apple Team ID (məs: ABC123XYZ)
        public string? PassTypeIdentifier { get; set; }       // pass.com.yourbrand.loyalty
        public IFormFile? AppleCertificateFile { get; set; }  // .p12 (upload yolu)
        public string? AppleCertificateUrl { get; set; }      // .p12 (artıq yüklənmiş fayla link/yol)
        public string? CertificatePassword { get; set; }      // .p12 parolu

        // WWDR .cer (opsional, lakin məsləhətlidir)
        public IFormFile? AppleWwdrCertFile { get; set; }     // .cer (upload yolu)
        public string? AppleWwdrCertUrl { get; set; }         // .cer (artıq mövcuddursa link/yol)

        // PassKit Web Service (push update üçün — opsional, gələcək üçün saxla)
        public string? PassKitServiceUrl { get; set; }        // https://tenant.example.com/passkit
        public string? PassKitAuthToken { get; set; }         // 32-64 simvolluq random token

        // -------- GOOGLE --------
        public string? IssuerId { get; set; }
        public string? ServiceAccountEmail { get; set; }
        public IFormFile? GooglePrivateKeyFile { get; set; }  // service account JSON
        public string? GooglePrivateKeyUrl { get; set; }      // artıq yüklənmiş JSON-a link/yol
    }
}

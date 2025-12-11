using Loyalty.DataAccess.Entities.Main;
using Loyalty.DataAccess.Enums;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Loyalty.Business.Dtos.Get.Main
{
    public class GetWalletCredentialDto
    {
        public int Id { get; set; }
        public int TenantId { get; set; }
        public WalletPlatform Platform { get; set; } // GooglePay = 1, AppleWallet = 2 ...

        // -------- Apple --------
        public string? TeamIdentifier { get; set; }          // Apple Developer Team ID (məs: ABC123XYZ)
        public string? PassTypeIdentifier { get; set; }      // Pass Type ID (məs: pass.com.yourbrand.loyalty)
        public string? AppleCertificateUrl { get; set; }     // .p12 faylının serverdə yolu/linki
        public string? CertificatePassword { get; set; }     // .p12 şifrəsi (ENCRYPT ETMƏYİ UNUTMA!)

        public string? AppleWwdrCertUrl { get; set; }        // WWDR .cer (opsional, məsləhətlidir)

        // Push Update (opsional, amma gələcək üçün saxla)
        public string? PassKitServiceUrl { get; set; }       // Məs: https://tenant.yoursaas.com/passkit
        public string? PassKitAuthToken { get; set; }        // Random 32+ simvol token

        // (Opsional) fayl adları/metadata
        public string? AppleCertFileName { get; set; }
        public string? AppleWwdrFileName { get; set; }

        // -------- Google --------
        public string? IssuerId { get; set; }
        public string? ServiceAccountEmail { get; set; }
        public string? GooglePrivateKeyUrl { get; set; }     // Service account JSON (yol/link)

        public bool IsActive { get; set; } = true;

        public Tenant Tenant { get; set; } = null!;
    }
}

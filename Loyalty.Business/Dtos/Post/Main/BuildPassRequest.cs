using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Loyalty.Business.Dtos.Post.Main
{
    public sealed class BuildPassRequest
    {
        // Required
        public string TeamIdentifier { get; set; } = null!;
        public string PassTypeIdentifier { get; set; } = null!;
        public string SerialNumber { get; set; } = null!;
        public string OrganizationName { get; set; } = "Walvero";
        public string Description { get; set; } = "Loyalty Card";
        public string? LogoText { get; set; }
        public string? Barcode { get; set; }
        public string? CustomerName { get; set; }

        // Colors as #RRGGBB or rgb(r,g,b)
        public string? BackgroundColor { get; set; }    // e.g. "#FFFFFF"
        public string? ForegroundColor { get; set; }    // optional
        public string? LabelColor { get; set; }         // optional

        // Barcode
        public string BarcodeMessage { get; set; } = "";
        public string BarcodeFormat { get; set; } = "PKBarcodeFormatCode128"; // Apple's enum string

        // Fields (StoreCard)
        public List<PassField> HeaderFields { get; set; } = new();
        public List<PassField> PrimaryFields { get; set; } = new();
        public List<PassField> SecondaryFields { get; set; } = new();
        public List<PassField> AuxiliaryFields { get; set; } = new();

        // Images (absolute URLs, builder yükləyib fayla çevirəcək)
        public string? LogoUrl { get; set; }
        public string? HeroImageUrl { get; set; }   // optional (strip)
        public string? StripImageUrl { get; set; }  // usually tier/progress icon

        // Optional – PassKit Web Service (push updates üçün)
        public string? WebServiceURL { get; set; }
        public string? AuthenticationToken { get; set; }
    }
}

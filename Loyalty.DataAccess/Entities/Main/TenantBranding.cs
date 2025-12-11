
using Loyalty.DataAccess.Abstractions;
using Loyalty.DataAccess.Entities.Base;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Loyalty.DataAccess.Entities.Main
{
    public class TenantBranding:EntityBase,IEntity
    {
        public int TenantId { get; set; }

        // Logo and Images
        public string? LogoUrl { get; set; }
        public string? BackgroundImageUrl { get; set; }
        public string? IconUrl { get; set; }

        // Colors (Hex format)
        public string PrimaryColor { get; set; } = "#000000";
        public string SecondaryColor { get; set; } = "#FFFFFF";
        public string BackgroundColor { get; set; } = "#FFFFFF";
        public string TextColor { get; set; } = "#000000";

        // Branding Text
        public string? Description { get; set; }
        public string? Website { get; set; }

        // Navigation Property
        public Tenant Tenant { get; set; } = null!;
    }
}

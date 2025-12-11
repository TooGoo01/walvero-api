using Loyalty.DataAccess.Abstractions;
using Loyalty.DataAccess.Entities.Base;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Loyalty.DataAccess.Entities.Main
{
    public class TenantUi : EntityBase, IEntity
    {
        public int TenantId { get; set; }
        public string PrimaryColor { get; set; } = "#000000";
        public string SecondaryColor { get; set; } = "#FFFFFF";
        public string AccentColor { get; set; } = "#CCCCCC";
        public string BackgroundColor { get; set; } = "#FFFFFF";
        public string DarkBgColor { get; set; } = "#0F0F10";
        public string LogoUrl { get; set; } = default!;
        public string? HeroUrl { get; set; }
        public string? FaviconUrl { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}

using Loyalty.DataAccess.Abstractions;
using Loyalty.DataAccess.Entities.Base;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Loyalty.DataAccess.Entities.Main
{
    public class MenuCategory:EntityBase, IEntity
    {
        public string AzName { get; set; }
        public string EnName { get; set; }
        public string RuName { get; set; }
        public string? AzDescription { get; set; }
        public string? EnDescription { get; set; }
        public string? RuDescription { get; set; }

        // Description style override; null = inherit tenant branding default
        public string? DescriptionColor { get; set; }       // hex #RRGGBB
        public int? DescriptionFontSize { get; set; }       // px, clamped 10-40

        // Admin-controlled display order. Default 0 → falls back to Id ordering.
        public int SortOrder { get; set; }

        public int TenantId { get; set; }
        public Tenant Tenant { get; set; } = null!;

        public int? BranchId { get; set; }
        public Branch? Branch { get; set; }

        public ICollection<MenuItem> MenuItems { get; set; } = new List<MenuItem>();
    }
}

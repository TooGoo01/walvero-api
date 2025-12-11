using Loyalty.DataAccess.Abstractions;
using Loyalty.DataAccess.Entities.Base;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Loyalty.DataAccess.Entities.Main
{
    public class TenantWalletApple : EntityBase, IEntity
    {
        public int TenantId { get; set; }
        public string BackgroundColor { get; set; } = "rgb(255,255,255)";
        public string LabelColor { get; set; } = "rgb(0,0,0)";
        public string ForegroundColor { get; set; } = "rgb(0,0,0)";
        public string PassTypeIdentifier { get; set; } = default!;
        public string TeamIdentifier { get; set; } = default!;
        public string? LogoText { get; set; }
        public DateTime UpdatedAt { get; set; }

        public ICollection<TenantWalletAppleField> Fields { get; set; } = new List<TenantWalletAppleField>();
        public TenantWalletAppleImage Images { get; set; } = default!;
    }
}

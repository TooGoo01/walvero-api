using Loyalty.DataAccess.Abstractions;
using Loyalty.DataAccess.Entities.Base;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Loyalty.DataAccess.Entities.Main
{
    public class TenantWalletGoogle : EntityBase, IEntity
    {
        public int TenantId { get; set; }
        public string ClassId { get; set; } = default!;
        public string HexPrimary { get; set; } = "#000000";
        public string HexSecondary { get; set; } = "#FFFFFF";
        public DateTime UpdatedAt { get; set; }

        public ICollection<TenantWalletGoogleText> Texts { get; set; } = new List<TenantWalletGoogleText>();
        public ICollection<TenantWalletGoogleLink> Links { get; set; } = new List<TenantWalletGoogleLink>();
    }
}

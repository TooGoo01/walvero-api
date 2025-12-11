using Loyalty.DataAccess.Abstractions;
using Loyalty.DataAccess.Entities.Base;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Loyalty.DataAccess.Entities.Main
{
    public class TenantWalletAppleImage : EntityBase, IEntity
    {
        public int TenantId { get; set; }
        public string? LogoUrl { get; set; }
        public string? IconUrl { get; set; }
        public string? StripUrl { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}

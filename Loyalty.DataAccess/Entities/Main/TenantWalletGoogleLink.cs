using Loyalty.DataAccess.Abstractions;
using Loyalty.DataAccess.Entities.Base;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Loyalty.DataAccess.Entities.Main
{
    public class TenantWalletGoogleLink : EntityBase, IEntity
    {
        public long Id { get; set; }
        public int TenantId { get; set; }
        public string Description { get; set; } = default!;
        public string Uri { get; set; } = default!;
    }
}

using Loyalty.DataAccess.Abstractions;
using Loyalty.DataAccess.Entities.Base;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Loyalty.DataAccess.Entities.Main
{
    public class TenantWalletGoogleText : EntityBase, IEntity
    {
        public long Id { get; set; }
        public int TenantId { get; set; }
        public string Header { get; set; } = default!;
        public string Body { get; set; } = default!;
    }
}

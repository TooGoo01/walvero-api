using Loyalty.DataAccess.Abstractions;
using Loyalty.DataAccess.Entities.Base;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Loyalty.DataAccess.Entities.Main
{
    public class TenantWalletAppleField : EntityBase, IEntity
    {
        public long Id { get; set; }
        public int TenantId { get; set; }
        public string Section { get; set; } = "primary"; // primary|auxiliary
        public string FieldKey { get; set; } = default!;
        public string Label { get; set; } = default!;
        public string? ValueFormat { get; set; }
        public string? DefaultValue { get; set; }
    }
}

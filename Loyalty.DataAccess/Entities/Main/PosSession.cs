using Loyalty.DataAccess.Abstractions;
using Loyalty.DataAccess.Entities.Base;
using Loyalty.DataAccess.Entities.Users;
using Loyalty.DataAccess.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Loyalty.DataAccess.Entities.Main
{
    public class PosSession : EntityBase, IEntity
    {
        public Tenant Tenant { get; set; }
        public int TenantId { get; set; }
        public string scannerDeviceId { get; set; }
        public ApplicationUser applicationUser { get; set; }
        public int userId { get; set; }
        public int? orderId { get; set; }
        public int? tableId { get; set; }
        public string? Barcode { get; set; }
        public SessionStatus Status { get; set; } = SessionStatus.Active;
        public string? SessionToken { get; set; }
    }
}

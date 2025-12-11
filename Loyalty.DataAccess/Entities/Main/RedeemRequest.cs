using Loyalty.DataAccess.Abstractions;
using Loyalty.DataAccess.Entities.Base;
using Loyalty.DataAccess.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Loyalty.DataAccess.Entities.Main
{
    public sealed class RedeemRequest : EntityBase, IEntity
    {
       
        public int TenantId { get; set; }
        public int LoyaltyCardId { get; set; }
        public int PointsDelta { get; set; }
        public RedeemStatus Status { get; set; } = RedeemStatus.Pending;
        public WalletPlatform Platform { get; set; }

        public string Code { get; set; } = default!; // OTP
      
        public DateTime ExpiresUtc { get; set; }
        public string? IdempotencyKey { get; set; }

        public string? OperatorId { get; set; }
        public string? OrderId { get; set; }
        public string? Note { get; set; }
    }
}

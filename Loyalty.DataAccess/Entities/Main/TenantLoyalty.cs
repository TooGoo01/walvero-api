using Loyalty.DataAccess.Abstractions;
using Loyalty.DataAccess.Entities.Base;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Loyalty.DataAccess.Entities.Main
{
    public class TenantLoyalty : EntityBase, IEntity
    {
        public int TenantId { get; set; }
        public int StartPoints { get; set; } = 0;
        public decimal PointPerAZN { get; set; } = 1m;
        public int RedeemThreshold { get; set; } = 50;
        public string? RewardDesc { get; set; }
        public string BarcodeFormat { get; set; } = "QR";
        public string BarcodePrefix { get; set; } = "";
        public DateTime UpdatedAt { get; set; }
    }
}


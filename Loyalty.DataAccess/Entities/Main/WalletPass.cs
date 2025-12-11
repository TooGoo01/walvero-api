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
    public class WalletPass:EntityBase,IEntity
    {
        public int LoyaltyCardId { get; set; }

        // Platform
        public WalletPlatform Platform { get; set; }

        // Pass Details
        public string SerialNumber { get; set; } = string.Empty;
        public string PassTypeIdentifier { get; set; } = string.Empty; // Apple specific
        public string? PassUrl { get; set; } // Download URL

        // Status
        public PassStatus Status { get; set; } = PassStatus.Active;
        public DateTime? LastUpdated { get; set; }
        public DateTime? InstalledAt { get; set; }

        // Navigation Property
        public LoyaltyCard LoyaltyCard { get; set; } = null!;
    }
}

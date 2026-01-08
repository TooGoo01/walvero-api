using Loyalty.DataAccess.Abstractions;
using Loyalty.DataAccess.Entities.Base;
using Loyalty.DataAccess.Entities.Users;
using Loyalty.DataAccess.Enums;
using System;
using System.ComponentModel.DataAnnotations;

namespace Loyalty.DataAccess.Entities.Main
{
    /// <summary>
    /// Stores wallet pass records for Walvero shadow tenant.
    /// These passes are generated based on user's WalveroId, not loyalty cards.
    /// </summary>
    public class WalveroWalletPass : EntityBase, IEntity
    {
        public int ApplicationUserId { get; set; }
        public int TenantId { get; set; } // Walvero shadow tenant ID

        // WalveroId (stored for reference, can be masked/encrypted if needed)
        public string WalveroIdMasked { get; set; } = string.Empty; // e.g., "WLV****123"

        // Platform-specific URLs
        public string? ApplePassUrl { get; set; } // iOS pass download URL
        
        [StringLength(2000)]
        public string? GoogleSaveUrl { get; set; } // Android save URL or objectId
        public string? AppleSerialNumber { get; set; } // Apple pass serial number
        public string? GoogleObjectId { get; set; } // Google Wallet object ID

        // Platform availability flags
        public bool HasIosPass { get; set; } = false;
        public bool HasAndroidPass { get; set; } = false;

        // Timestamps
        public DateTime? LastGeneratedAt { get; set; }

        // Navigation Properties
        public ApplicationUser ApplicationUser { get; set; } = null!;
        public Tenant Tenant { get; set; } = null!;
    }
}


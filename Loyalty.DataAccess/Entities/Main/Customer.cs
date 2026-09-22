using Loyalty.DataAccess.Abstractions;
using Loyalty.DataAccess.Entities.Base;
using Loyalty.DataAccess.Entities.Users;
using Loyalty.DataAccess.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Loyalty.DataAccess.Entities.Main
{
    public class Customer : EntityBase, IEntity
    {
        public int TenantId { get; set; }

        // Personal Information
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty; // UNIQUE per tenant
        public string? Email { get; set; }
        public string? OtpHash { get; set; }          // SHA256( salt + code )
        public string? OtpSalt { get; set; }          // Random 16-32 byte base64
        public DateTime? OtpExpiresAt { get; set; }   // bitmə vaxtı
        public int OtpAttempts { get; set; }          // uğursuz yoxlama sayğacı
        public DateTime? OtpLastSentAt { get; set; }  // throttling üçün
        public string? OtpChannel { get; set; }       // "sms" / "email"
        public string? OtpDestinationMasked { get; set; }
        public Gender? Gender { get; set; }
        public DateTime? DateOfBirth { get; set; }

        // Status
        public bool IsActive { get; set; } = true;
        public DateTime? LastActivityDate { get; set; }

        // Ambassador (promoter) — tenant admin tərəfindən yaradılan xüsusi müştəri.
        // Admin paneldə ad yanında işarə ilə fərqlənir.
        public bool IsAmbassador { get; set; } = false;

        // Card Number - auto-generated per tenant with dynamic prefix
        public string? CardNumber { get; set; }

        // Navigation Properties
        public Tenant Tenant { get; set; } = null!;
        public int? applicationUserId { get; set; } = null!;
        public ApplicationUser? applicationUser { get; set; } = null!;
        public ICollection<LoyaltyCard> LoyaltyCards { get; set; } = new List<LoyaltyCard>();
    }
}

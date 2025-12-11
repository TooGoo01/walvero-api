using Loyalty.DataAccess.Abstractions;
using Loyalty.DataAccess.Entities.Base;
using Loyalty.DataAccess.Enums;

namespace Loyalty.DataAccess.Entities.Users
{
    public class Otp : EntityBase, IEntity
    {
        public int ApplicationUserId { get; set; }
        public string CodeHash { get; set; } = string.Empty;
        public string CodeSalt { get; set; } = string.Empty;
        public DateTime ExpiresAt { get; set; }
        public int AttemptsCount { get; set; } = 0;
        public bool IsUsed { get; set; } = false;
        public OtpPurpose Purpose { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UsedAt { get; set; }

        // Navigation Properties
        public ApplicationUser User { get; set; } = null!;

        // Computed properties
        public bool IsExpired => DateTime.UtcNow >= ExpiresAt;
        public bool IsValid => !IsUsed && !IsExpired && AttemptsCount < 5;
    }
}


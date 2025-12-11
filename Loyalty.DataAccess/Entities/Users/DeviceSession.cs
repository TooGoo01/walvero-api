using Loyalty.DataAccess.Abstractions;
using Loyalty.DataAccess.Entities.Base;

namespace Loyalty.DataAccess.Entities.Users
{
    public class DeviceSession : EntityBase, IEntity
    {
        public int ApplicationUserId { get; set; }
        public string DeviceId { get; set; } = string.Empty;
        public bool IsGuest { get; set; }
        public bool IsRevoked { get; set; } = false;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime LastSeenAt { get; set; } = DateTime.UtcNow;

        // Navigation Properties
        public ApplicationUser User { get; set; } = null!;
    }
}


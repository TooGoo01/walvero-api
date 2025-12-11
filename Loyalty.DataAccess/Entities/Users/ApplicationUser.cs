using Loyalty.DataAccess.Entities.Main;
using Loyalty.DataAccess.Enums;
using Microsoft.AspNetCore.Identity;

namespace Loyalty.DataAccess.Entities.Users
{
    public class ApplicationUser : IdentityUser<int>
    {
        public string Name { get; set; }
        public string Surname { get; set; }
        public string Position { get; set; }
        public Gender? Gender { get; set; }
        public DateTime? DateOfBirth { get; set; }
        public int? TenantId { get; set; }
        public Tenant? Tenant { get; set; }
        public ICollection<DeviceToken>? DeviceTokens { get; set; } = new List<DeviceToken>();
        public ICollection<Customer>? Customers { get; set; }
        
        // Guest user properties
        public bool IsGuest { get; set; } = false;
        public bool IsProfileCompleted { get; set; } = false;
        public DateTime? GuestCreatedAt { get; set; }
        public DateTime? ProfileCompletedAt { get; set; }
        public string? GuestDeviceId { get; set; }
        public DateTime? LastProfileReminderAt { get; set; }
        
        // Navigation property for DeviceSession
        public ICollection<DeviceSession>? DeviceSessions { get; set; } = new List<DeviceSession>();
        
        // Navigation property for RefreshToken
        public ICollection<RefreshToken>? RefreshTokens { get; set; } = new List<RefreshToken>();
        
        // Phone verification properties
        // Note: Status defaults to Active for backward compatibility with existing users
        // Existing users will remain Active after migration
        public bool IsPhoneVerified { get; set; } = false;
        public UserStatus Status { get; set; } = UserStatus.Active; // Default to Active for backward compatibility
        
        // Navigation properties for OTP and PasswordResetToken
        public ICollection<Otp>? Otps { get; set; } = new List<Otp>();
        public ICollection<PasswordResetToken>? PasswordResetTokens { get; set; } = new List<PasswordResetToken>();
        
        // WalveroId - custom ID per country (e.g., 994100001, 994100002, ...)
        public long? WalveroId { get; set; }
    }
}

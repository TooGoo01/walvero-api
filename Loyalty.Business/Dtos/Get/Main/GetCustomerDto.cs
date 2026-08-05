using Loyalty.DataAccess.Entities.Main;
using Loyalty.DataAccess.Entities.Users;
using Loyalty.DataAccess.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Loyalty.Business.Dtos.Get.Main
{
    public class GetCustomerDto
    {
        public int Id { get; set; }
        public int TenantId { get; set; }
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
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
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? LastActivityDate { get; set; }
        public string? CardNumber { get; set; }
        public int? applicationUserId { get; set; } = null!;
        public ApplicationUser? applicationUser { get; set; } = null!;
        public ICollection<GetLoyaltyCardDto> LoyaltyCards { get; set; }
        public string? Currency { get; set; }  // Currency from first active card's CardTemplate
    }
}

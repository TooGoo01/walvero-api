using Loyalty.DataAccess.Entities.Users;
using Loyalty.DataAccess.Enums;

namespace Loyalty.Business.Dtos.Get.Main
{
    /// <summary>
    /// /api/Customer/List endpoint-i üçün DTO. GetCustomerDto-nun klonu, nested LoyaltyCards
    /// GetLoyaltyCardListItemDto istifadə edir (points decimal).
    /// </summary>
    public class GetCustomerListItemDto
    {
        public int Id { get; set; }
        public int TenantId { get; set; }
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public string? Email { get; set; }
        public string? OtpHash { get; set; }
        public string? OtpSalt { get; set; }
        public DateTime? OtpExpiresAt { get; set; }
        public int OtpAttempts { get; set; }
        public DateTime? OtpLastSentAt { get; set; }
        public string? OtpChannel { get; set; }
        public string? OtpDestinationMasked { get; set; }
        public Gender? Gender { get; set; }
        public DateTime? DateOfBirth { get; set; }
        public bool IsActive { get; set; }
        public bool IsAmbassador { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? LastActivityDate { get; set; }
        public string? CardNumber { get; set; }
        public int? applicationUserId { get; set; }
        public ApplicationUser? applicationUser { get; set; }
        public ICollection<GetLoyaltyCardListItemDto> LoyaltyCards { get; set; } = new List<GetLoyaltyCardListItemDto>();
        public string? Currency { get; set; }
    }
}

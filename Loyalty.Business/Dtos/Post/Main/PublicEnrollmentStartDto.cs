using System.ComponentModel.DataAnnotations;

namespace Loyalty.Business.Dtos.Post.Main
{
    public sealed class PublicEnrollmentStartDto
    {
        [Required]
        public int TenantId { get; set; }

        [Required]
        public int LoyaltyProgramId { get; set; }

        [Required]
        [StringLength(50)]
        public string FirstName { get; set; } = "";

        [StringLength(50)]
        public string? LastName { get; set; }

        [Required]
        [Phone]
        public string PhoneNumber { get; set; } = "";

        [EmailAddress]
        public string? Email { get; set; }

        public int? Gender { get; set; }

        public DateTime? DateOfBirth { get; set; }

        public string? DevicePlatform { get; set; } = "";
    }
}

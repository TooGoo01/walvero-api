using System.ComponentModel.DataAnnotations;

namespace Loyalty.Business.Dtos.Post.Users
{
    public class CompleteProfileDto
    {
        [Required]
        [StringLength(100, MinimumLength = 1)]
        public string FullName { get; set; } = string.Empty;

        [Required]
        [StringLength(20, MinimumLength = 1)]
        public string PhoneNumber { get; set; } = string.Empty;

        [EmailAddress]
        public string? Email { get; set; }
    }
}


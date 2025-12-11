using System.ComponentModel.DataAnnotations;

namespace Loyalty.Business.Dtos.Post.Users
{
    public class RegisterWithPhoneDto
    {
        [Required]
        public string PhoneNumber { get; set; } = string.Empty; // With country code
        
        [Required]
        [MinLength(4)]
        public string Password { get; set; } = string.Empty;
        
        [Compare("Password", ErrorMessage = "Passwords do not match")]
        public string ConfirmPassword { get; set; } = string.Empty;
        
        public string? Name { get; set; }
        public string? Surname { get; set; }
        public string? Email { get; set; }
    }
}


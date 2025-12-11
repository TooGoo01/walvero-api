using System.ComponentModel.DataAnnotations;

namespace Loyalty.Business.Dtos.Post.Users
{
    public class ResetPasswordDto
    {
        [Required]
        public string ResetToken { get; set; } = string.Empty;
        
        [Required]
        [MinLength(4)]
        public string NewPassword { get; set; } = string.Empty;
        
        [Compare("NewPassword", ErrorMessage = "Passwords do not match")]
        public string ConfirmPassword { get; set; } = string.Empty;
    }
}


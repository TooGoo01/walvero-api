using System.ComponentModel.DataAnnotations;

namespace Loyalty.Business.Dtos.Post.Users
{
    public class VerifyOtpDto
    {
        [Required]
        public int UserId { get; set; }
        
        [Required]
        [StringLength(6, MinimumLength = 4)]
        public string OtpCode { get; set; } = string.Empty;
        
        [Required]
        public string Purpose { get; set; } = "Registration"; // "Registration" or "PasswordReset"
    }
}


using System.ComponentModel.DataAnnotations;

namespace Loyalty.Business.Dtos.Post.Users
{
    public class VerifyOtpRequest
    {
        [Required]
        public int UserId { get; set; }
        
        [Required]
        [StringLength(6, MinimumLength = 4)]
        public string OtpCode { get; set; } = null!;
        
        [Required]
        public string Purpose { get; set; } = null!;  // "Registration" or "Login" (and maybe "PasswordReset" in future)
    }
}


using System.ComponentModel.DataAnnotations;

namespace Loyalty.Business.Dtos.Post.Users
{
    public class VerifyForgotPasswordOtpDto
    {
        [Required]
        public string PhoneNumber { get; set; } = string.Empty;
        
        [Required]
        [StringLength(6, MinimumLength = 4)]
        public string OtpCode { get; set; } = string.Empty;
    }
}


using System.ComponentModel.DataAnnotations;

namespace Loyalty.Business.Dtos.Post.Users
{
    public class VerifyNewPhoneDto
    {
        [Required]
        public string NewPhoneNumber { get; set; } = string.Empty;
        
        [Required]
        public string OtpCode { get; set; } = string.Empty;
    }
}

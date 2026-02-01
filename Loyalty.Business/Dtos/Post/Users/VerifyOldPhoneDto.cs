using System.ComponentModel.DataAnnotations;

namespace Loyalty.Business.Dtos.Post.Users
{
    public class VerifyOldPhoneDto
    {
        [Required]
        public string OtpCode { get; set; } = string.Empty;
    }
}

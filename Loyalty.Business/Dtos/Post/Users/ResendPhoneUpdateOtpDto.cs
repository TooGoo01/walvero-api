using System.ComponentModel.DataAnnotations;

namespace Loyalty.Business.Dtos.Post.Users
{
    public class ResendPhoneUpdateOtpDto
    {
        [Required]
        public string OtpType { get; set; } = string.Empty; // "OldPhone" or "NewPhone"
        
        public string? NewPhoneNumber { get; set; } // Yalnız NewPhone üçün lazımdır
    }
}

using System.ComponentModel.DataAnnotations;

namespace Loyalty.Business.Dtos.Post.Users
{
    public class ForgotPasswordDto
    {
        [Required]
        public string PhoneNumber { get; set; } = string.Empty; // With country code

        /// <summary>"sms" → məcburi SMS; boş → WhatsApp-first.</summary>
        public string? Channel { get; set; }
    }
}


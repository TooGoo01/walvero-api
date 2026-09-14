using System.ComponentModel.DataAnnotations;

namespace Loyalty.Business.Dtos.Post.Users
{
    public class ResendOtpRequest
    {
        [Required]
        public int UserId { get; set; }
        
        [Required]
        public string Purpose { get; set; } = null!;  // "Registration" or "Login"

        /// <summary>"sms" → məcburi SMS; boş/digər → WhatsApp-first.</summary>
        public string? Channel { get; set; }
    }
}


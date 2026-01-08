using System.ComponentModel.DataAnnotations;

namespace Loyalty.Business.Dtos.Post.Users
{
    public class LoginRequest
    {
        [Required]
        public string Phone { get; set; } = null!;
        
        [Required]
        public string Password { get; set; } = null!;
    }
}


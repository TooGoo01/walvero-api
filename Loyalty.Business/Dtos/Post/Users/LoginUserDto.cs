using System.ComponentModel.DataAnnotations;

namespace Loyalty.Business.Dtos.Post.Users
{
    public class LoginUserDto
    {
        // Artıq optional - guest login üçün lazım deyil
        public string? UserName { get; set; }
        public string? Password { get; set; }
        
        // Guest login üçün
     
        public string DeviceId { get; set; } = string.Empty;
    }
}

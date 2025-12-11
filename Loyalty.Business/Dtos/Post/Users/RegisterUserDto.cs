using Loyalty.DataAccess.Enums;
using System.ComponentModel.DataAnnotations;

namespace Loyalty.Business.Dtos.Post.Users
{
    public class RegisterUserDto
    {
        // Guest login üçün DeviceId tələb olunur
        [Required]
        public string DeviceId { get; set; } = string.Empty;
       
        // Profil tamamlama üçün (optional - guest login zamanı lazım deyil)
        public string? Name { get; set; }
        public string? Surname { get; set; }
        [DataType(DataType.EmailAddress)]
        public string? Email { get; set; }
        public string? Phone { get; set; }
        public Gender? Gender { get; set; }
        public DateTime? DateOfBirth { get; set; }
        public string? Password { get; set; }
        [Compare("Password", ErrorMessage = "Şifrələr eyni deyil")]
        public string? ConfirmPassword { get; set; }
    }
}

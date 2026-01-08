using Loyalty.DataAccess.Enums;
using System.ComponentModel.DataAnnotations;

namespace Loyalty.Business.Dtos.Post.Users
{
    public class RegisterRequest
    {
        [Required]
        public string Phone { get; set; } = null!;  // includes country code, e.g. "994501234567"
        
        [Required]
        [MinLength(4)]
        public string Password { get; set; } = null!;
        
        [Compare("Password", ErrorMessage = "Şifrələr eyni deyil")]
        public string? ConfirmPassword { get; set; }
        
        public string? Name { get; set; }
        public string? Surname { get; set; }
        
        [DataType(DataType.EmailAddress)]
        [EmailAddress]
        public string? Email { get; set; }
        
        public Gender? Gender { get; set; }
        
        public DateTime? DateOfBirth { get; set; }
    }
}


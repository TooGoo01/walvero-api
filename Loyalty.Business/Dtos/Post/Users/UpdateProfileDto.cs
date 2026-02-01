using Loyalty.DataAccess.Enums;
using System.ComponentModel.DataAnnotations;

namespace Loyalty.Business.Dtos.Post.Users
{
    public class UpdateProfileDto
    {
        public string? Name { get; set; }
        public string? Surname { get; set; }
        
        [EmailAddress]
        public string? Email { get; set; }
        
        public string? PhoneNumber { get; set; } // Yeni telefon nömrəsi (opsional)
        
        public Gender? Gender { get; set; }
        
        public DateTime? DateOfBirth { get; set; }
    }
}

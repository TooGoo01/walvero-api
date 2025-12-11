using System.ComponentModel.DataAnnotations;

namespace Loyalty.Business.Dtos.Post.Users
{
    public class GuestLoginDto
    {
        [Required]
        public string DeviceId { get; set; } = string.Empty;

        // QEYD: TenantId artıq lazım deyil
        // Guest user hələ tenant seçməyib, tenant-a qoşulanda (məsələn, "My Cards" bölməsində)
        // CustomerEnrollmentService vasitəsilə tenant-a qoşulacaq
        // public int? TenantId { get; set; } // REMOVED - artıq istifadə olunmur
    }
}


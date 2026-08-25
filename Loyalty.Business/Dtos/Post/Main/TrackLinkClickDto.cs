using System.ComponentModel.DataAnnotations;

namespace Loyalty.Business.Dtos.Post.Main
{
    /// <summary>Mobil app-dan link keçidini qeyd etmək üçün (anonim).</summary>
    public class TrackLinkClickDto
    {
        [Required]
        public int TenantId { get; set; }

        public int? BranchId { get; set; }

        [Required]
        [MaxLength(50)]
        public string LinkType { get; set; } = string.Empty;
    }
}

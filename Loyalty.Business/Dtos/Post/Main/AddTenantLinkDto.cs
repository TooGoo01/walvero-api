using System.ComponentModel.DataAnnotations;
using Loyalty.Business.Dtos.Get.Main;

namespace Loyalty.Business.Dtos.Post.Main
{
    public class AddTenantLinkDto
    {
        [Required(ErrorMessage = "Tenant məcburidir")]
        public int TenantId { get; set; }

        public int? BranchId { get; set; }

        [Required(ErrorMessage = "Az başlıq məcburidir")]
        [MaxLength(200)]
        public string AzTitle { get; set; } = string.Empty;

        [MaxLength(200)]
        public string? EnTitle { get; set; }

        [MaxLength(200)]
        public string? RuTitle { get; set; }

        // Aksiya varsa URL ixtiyari ola bilər (popup ilə açılır)
        [MaxLength(1000)]
        public string? Url { get; set; }

        [MaxLength(50)]
        public string? IconKey { get; set; }

        public int SortOrder { get; set; }
        public bool OpenInNewTab { get; set; } = true;

        public List<LinkActionDto>? Actions { get; set; }
    }
}

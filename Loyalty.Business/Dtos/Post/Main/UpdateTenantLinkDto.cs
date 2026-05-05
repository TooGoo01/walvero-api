using System.ComponentModel.DataAnnotations;

namespace Loyalty.Business.Dtos.Post.Main
{
    public class UpdateTenantLinkDto
    {
        [Required(ErrorMessage = "Id məcburidir")]
        public int Id { get; set; }

        [Required(ErrorMessage = "Tenant məcburidir")]
        public int TenantId { get; set; }

        [Required(ErrorMessage = "Az başlıq məcburidir")]
        [MaxLength(200)]
        public string AzTitle { get; set; } = string.Empty;

        [MaxLength(200)]
        public string? EnTitle { get; set; }

        [MaxLength(200)]
        public string? RuTitle { get; set; }

        [Required(ErrorMessage = "URL məcburidir")]
        [MaxLength(1000)]
        public string Url { get; set; } = string.Empty;

        [MaxLength(50)]
        public string? IconKey { get; set; }

        public int SortOrder { get; set; }
        public bool OpenInNewTab { get; set; } = true;
        public bool IsActive { get; set; } = true;
    }
}

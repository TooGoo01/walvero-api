using System.ComponentModel.DataAnnotations;

namespace Loyalty.Business.Dtos.Post.Main
{
    public class ReorderTenantLinksDto
    {
        [Required]
        public int TenantId { get; set; }

        [Required]
        public List<TenantLinkOrderItem> Items { get; set; } = new();
    }

    public class TenantLinkOrderItem
    {
        public int Id { get; set; }
        public int SortOrder { get; set; }
    }
}

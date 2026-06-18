using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Loyalty.Business.Dtos.Post.Main
{
    public sealed class ReorderMenuItem
    {
        [Required] public int Id { get; set; }
        public int SortOrder { get; set; }
    }

    public sealed class ReorderMenuCategoryDto
    {
        public int TenantId { get; set; }
        public int? BranchId { get; set; }
        public List<ReorderMenuItem> Items { get; set; } = new();
    }

    public sealed class ReorderMenuItemDto
    {
        public int TenantId { get; set; }
        public int? BranchId { get; set; }
        public int MenuCategoryId { get; set; }
        public List<ReorderMenuItem> Items { get; set; } = new();
    }
}

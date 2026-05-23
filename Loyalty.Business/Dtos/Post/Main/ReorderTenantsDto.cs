using System.Collections.Generic;

namespace Loyalty.Business.Dtos.Post.Main
{
    public class ReorderTenantsDto
    {
        public List<TenantOrderItem> Items { get; set; } = new();
    }

    public class TenantOrderItem
    {
        public int Id { get; set; }
        public int SortOrder { get; set; }
    }
}

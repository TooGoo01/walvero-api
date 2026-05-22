using Loyalty.DataAccess.Abstractions;
using Loyalty.DataAccess.Entities.Base;

namespace Loyalty.DataAccess.Entities.Main
{
    public class TenantLink : EntityBase, IEntity
    {
        public int TenantId { get; set; }
        public Tenant Tenant { get; set; } = default!;

        public int? BranchId { get; set; }
        public Branch? Branch { get; set; }

        public string AzTitle { get; set; } = default!;
        public string? EnTitle { get; set; }
        public string? RuTitle { get; set; }

        public string Url { get; set; } = default!;
        public string? IconKey { get; set; }
        public int SortOrder { get; set; }
        public bool OpenInNewTab { get; set; } = true;
    }
}

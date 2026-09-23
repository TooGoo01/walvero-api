using Loyalty.DataAccess.Abstractions;
using Loyalty.DataAccess.Entities.Base;

namespace Loyalty.DataAccess.Entities.Main
{
    /// <summary>
    /// Tenant admin bildiriş göndərişlərinin logu — aylıq kvota sayğacı + tarixçə.
    /// Hər göndəriş aksiyası (hamısı/filial/fərdi) = 1 sətir.
    /// </summary>
    public class TenantNotificationLog : EntityBase, IEntity
    {
        public int TenantId { get; set; }

        /// <summary>"tenant" | "branch" | "customer"</summary>
        public string Scope { get; set; } = "tenant";
        public int? BranchId { get; set; }
        public int? CustomerId { get; set; }

        public string Title { get; set; } = string.Empty;
        public int RecipientCount { get; set; }
    }
}

using Loyalty.DataAccess.Abstractions;
using Loyalty.DataAccess.Entities.Base;

namespace Loyalty.DataAccess.Entities.Main
{
    /// <summary>
    /// Uğursuz iiko webhook sifarişi — dövri retry (Hangfire) üçün log.
    /// Müştəri sonradan sync olunanda sifariş yenidən emal olunub keçir.
    /// Status: 0=Pending, 1=Resolved, 2=Abandoned.
    /// </summary>
    public class IikoWebhookFailure : EntityBase, IEntity
    {
        public string? TerminalGroupId { get; set; }
        public string? OrderId { get; set; }
        public string? EventType { get; set; }   // OrderClosed / OrderReturned
        public string RawBody { get; set; } = string.Empty;
        public string? FailureReason { get; set; }
        public int AttemptCount { get; set; }
        public int Status { get; set; }           // 0=Pending, 1=Resolved, 2=Abandoned
        public DateTime? LastAttemptAt { get; set; }
        public DateTime? ResolvedAt { get; set; }
    }
}

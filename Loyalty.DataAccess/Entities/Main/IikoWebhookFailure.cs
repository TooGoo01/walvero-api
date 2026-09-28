using Loyalty.DataAccess.Abstractions;
using Loyalty.DataAccess.Entities.Base;

namespace Loyalty.DataAccess.Entities.Main
{
    /// <summary>
    /// Uğursuz iiko webhook sifarişi — dövri retry (Hangfire) üçün log.
    /// Status: bax <see cref="Enums.IikoWebhookFailureStatus"/> (0 Pending, 1 Resolved, 2 Abandoned, 3 Rejected).
    /// FailureKind: bax <see cref="Enums.IikoWebhookFailureKind"/> (1 Transient, 2 Retriable, 3 Permanent; 0 köhnə sətir).
    /// Retry uyğunluğu <see cref="NextAttemptAt"/> ilə (növə görə backoff), cəhd sayı <see cref="AttemptCount"/>.
    /// </summary>
    public class IikoWebhookFailure : EntityBase, IEntity
    {
        public string? TerminalGroupId { get; set; }
        public string? OrderId { get; set; }
        public string? EventType { get; set; }   // OrderClosed / OrderReturned
        public string RawBody { get; set; } = string.Empty;
        public string? FailureReason { get; set; }
        public int AttemptCount { get; set; }
        public int Status { get; set; }           // IikoWebhookFailureStatus
        public int FailureKind { get; set; }      // IikoWebhookFailureKind
        public DateTime? LastAttemptAt { get; set; }
        public DateTime? NextAttemptAt { get; set; } // Pending sətir yalnız bu vaxt çatanda retry olunur
        public DateTime? ResolvedAt { get; set; }
    }
}

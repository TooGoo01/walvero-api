using Loyalty.DataAccess.Abstractions;
using Loyalty.DataAccess.Entities.Base;

namespace Loyalty.DataAccess.Entities.Main
{
    /// <summary>
    /// Tenant admin bildiriş göndərişlərinin logu — aylıq kvota sayğacı + tarixçə.
    /// Hər göndəriş aksiyası (hamısı/filial/fərdi/filtrli, planlı) = 1 sətir; ad günü job-u gündə 1 sətir (Source="birthday", kvotaya sayılmır).
    /// </summary>
    public class TenantNotificationLog : EntityBase, IEntity
    {
        public int TenantId { get; set; }

        /// <summary>"tenant" | "branch" | "customer" | "filtered" | "birthday"</summary>
        public string Scope { get; set; } = "tenant";
        public int? BranchId { get; set; }
        public int? CustomerId { get; set; }

        public string Title { get; set; } = string.Empty;
        public int RecipientCount { get; set; }

        // ---- v2 (2026-10-02) ----
        public const string SourceManual = "manual";
        public const string SourceScheduled = "scheduled";
        public const string SourceBirthday = "birthday";

        /// <summary>"manual" | "scheduled" | "birthday" (köhnə sətirlər "manual").</summary>
        public string Source { get; set; } = SourceManual;
        public string? Body { get; set; }
        /// <summary>"app", "wallet", "app,wallet"</summary>
        public string? Channels { get; set; }
        public int? ScheduledNotificationId { get; set; }
        /// <summary>İnsan oxuya bilən auditoriya xülasəsi (məs. "Qadın · 18–25 yaş · son 30 gündə gələnlər").</summary>
        public string? AudienceSummary { get; set; }
        public int? AppSent { get; set; }
        public int? WalletSent { get; set; }
    }
}

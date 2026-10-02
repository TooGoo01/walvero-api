using Loyalty.DataAccess.Abstractions;
using Loyalty.DataAccess.Entities.Base;

namespace Loyalty.DataAccess.Entities.Main
{
    public enum ScheduledNotificationStatus
    {
        Pending = 0,
        Sending = 1,
        Sent = 2,
        Cancelled = 3,
        Failed = 4
    }

    /// <summary>
    /// Tenant admin-in gələcək tarixə planlaşdırdığı bildiriş. Recurring job ("scheduled-notifications", hər dəqiqə)
    /// vaxtı çatanı atomik götürür (Pending→Sending), auditoriyanı GÖNDƏRİŞ ANINDA hesablayır və göndərir.
    /// </summary>
    public class ScheduledNotification : EntityBase, IEntity
    {
        public int TenantId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Body { get; set; } = string.Empty;
        /// <summary>AudienceFilterDto JSON (filial/fərdi/cins/yaş/aktivlik/balans/kanal).</summary>
        public string AudienceJson { get; set; } = "{}";
        public bool ToApp { get; set; } = true;
        public bool ToWallet { get; set; }
        public DateTime ScheduledAtUtc { get; set; }
        public ScheduledNotificationStatus Status { get; set; } = ScheduledNotificationStatus.Pending;
        public DateTime? SentAtUtc { get; set; }
        public int? RecipientCount { get; set; }
        public string? Error { get; set; }
        /// <summary>Branch Admin/Moderator yaradıbsa — göndərişdə bu filiala məhdudlaşdırılır.</summary>
        public int? BranchScopeId { get; set; }
        /// <summary>"İndi göndər" — sorğu içində dərhal emal olunur (log Source = manual); false = planlı.</summary>
        public bool IsImmediate { get; set; }
    }
}

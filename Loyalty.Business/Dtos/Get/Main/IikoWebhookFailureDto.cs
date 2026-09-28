namespace Loyalty.Business.Dtos.Get.Main
{
    /// <summary>Super admin: uğursuz iiko webhook sətri (RawBody-siz).</summary>
    public class IikoWebhookFailureDto
    {
        public int Id { get; set; }
        public string? TerminalGroupId { get; set; }
        public string? OrderId { get; set; }
        public string? EventType { get; set; }
        public int Status { get; set; }
        public string StatusName { get; set; } = string.Empty;      // Pending / Resolved / Abandoned / Rejected
        public int FailureKind { get; set; }
        public string FailureKindName { get; set; } = string.Empty; // Unknown / Transient / Retriable / Permanent
        public int AttemptCount { get; set; }
        public string? FailureReason { get; set; }
        public DateTime RegDate { get; set; }
        public DateTime? LastAttemptAt { get; set; }
        public DateTime? NextAttemptAt { get; set; }
        public DateTime? ResolvedAt { get; set; }
    }

    /// <summary>Tək sətir — orijinal payload ilə.</summary>
    public class IikoWebhookFailureDetailDto : IikoWebhookFailureDto
    {
        public string RawBody { get; set; } = string.Empty;
    }
}

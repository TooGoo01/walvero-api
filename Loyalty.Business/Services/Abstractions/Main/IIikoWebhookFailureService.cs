using Loyalty.Business.Dtos.Get.Main;

namespace Loyalty.Business.Services.Abstractions.Main
{
    /// <summary>
    /// Uğursuz iiko webhook sifarişlərini log kimi saxlayır və Hangfire ilə
    /// növə görə (Transient / Retriable / Permanent) backoff ilə yenidən emal edir.
    /// Permanent uğursuzluq Rejected olur — heç vaxt retry olunmur.
    /// </summary>
    public interface IIikoWebhookFailureService
    {
        /// <summary>
        /// Uğursuz sifarişi qeyd et (OrderId+EventType üzrə upsert). Boş OrderId → yalnız log.
        /// kind: Permanent → Rejected; Transient/Retriable → Pending + NextAttemptAt (backoff).
        /// </summary>
        Task RecordFailureAsync(IikoOrderEventRequest req, int? tenantIntegrationId, string? reason, IikoFailureKind kind, CancellationToken ct = default);

        /// <summary>Hangfire recurring: NextAttemptAt çatmış Pending sifarişləri yenidən emal et.</summary>
        Task RetryPendingAsync(CancellationToken ct = default);

        // ---- Super admin ----
        Task<List<IikoWebhookFailureDto>> ListAsync(int? status, string? terminalGroupId, int take, CancellationToken ct = default);
        Task<IikoWebhookFailureDetailDto?> GetAsync(int id, CancellationToken ct = default);
        /// <summary>Sətri dərhal növbəyə qaytar (AttemptCount sıfırlanır). Tapılmadı → false.</summary>
        Task<bool> RequeueAsync(int id, CancellationToken ct = default);
        /// <summary>Terminal qrupunun Pending/Abandoned/Rejected sətirlərini toplu requeue et; sayı qaytarır.</summary>
        Task<int> RequeueByTerminalGroupAsync(string terminalGroupId, CancellationToken ct = default);
    }
}

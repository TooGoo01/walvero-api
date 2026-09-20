namespace Loyalty.Business.Services.Abstractions.Main
{
    /// <summary>
    /// Uğursuz iiko webhook sifarişlərini log kimi saxlayır və Hangfire ilə
    /// dövri yenidən emal edir (customer sonradan sync olunanda keçsin).
    /// </summary>
    public interface IIikoWebhookFailureService
    {
        /// <summary>Uğursuz sifarişi qeyd et (OrderId+EventType üzrə upsert).</summary>
        Task RecordFailureAsync(IikoOrderEventRequest req, int? tenantIntegrationId, string? reason, CancellationToken ct = default);

        /// <summary>Hangfire recurring: Pending sifarişləri yenidən emal et.</summary>
        Task RetryPendingAsync(CancellationToken ct = default);
    }
}

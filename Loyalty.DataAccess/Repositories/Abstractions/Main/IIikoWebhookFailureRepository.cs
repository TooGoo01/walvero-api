using Loyalty.DataAccess.Entities.Main;

namespace Loyalty.DataAccess.Repositories.Abstractions.Main
{
    public interface IIikoWebhookFailureRepository : IGenericRepository<IikoWebhookFailure>
    {
        /// <summary>OrderId + EventType üzrə mövcud qeydi tap (upsert üçün).</summary>
        Task<IikoWebhookFailure?> GetByOrderAsync(string? orderId, string? eventType, CancellationToken ct = default);

        /// <summary>Retry üçün Pending sətirlər (attempt/age limitli).</summary>
        Task<List<IikoWebhookFailure>> GetPendingAsync(int maxAttempts, DateTime minRegDate, int take, CancellationToken ct = default);
    }
}

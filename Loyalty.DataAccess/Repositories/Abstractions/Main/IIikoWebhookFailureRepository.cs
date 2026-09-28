using Loyalty.DataAccess.Entities.Main;

namespace Loyalty.DataAccess.Repositories.Abstractions.Main
{
    public interface IIikoWebhookFailureRepository : IGenericRepository<IikoWebhookFailure>
    {
        /// <summary>OrderId + EventType üzrə mövcud qeydi tap (upsert üçün).</summary>
        Task<IikoWebhookFailure?> GetByOrderAsync(string? orderId, string? eventType, CancellationToken ct = default);

        /// <summary>Retry vaxtı çatmış Pending sətirlər (NextAttemptAt &lt;= now, yaş limitli).</summary>
        Task<List<IikoWebhookFailure>> GetDueAsync(DateTime nowUtc, DateTime minRegDate, int take, CancellationToken ct = default);

        /// <summary>Admin siyahısı: status/terminal filtrli, ən yeni əvvəl.</summary>
        Task<List<IikoWebhookFailure>> ListAsync(int? status, string? terminalGroupId, int take, CancellationToken ct = default);

        /// <summary>Terminal qrupu üzrə verilmiş statuslarda sətirlər (toplu requeue üçün).</summary>
        Task<List<IikoWebhookFailure>> GetByTerminalGroupAsync(string terminalGroupId, List<int> statuses, CancellationToken ct = default);
    }
}

using Loyalty.DataAccess.Entities.Main;

namespace Loyalty.DataAccess.Repositories.Abstractions.Main
{
    public interface ITelegramAdminLinkRepository : IGenericRepository<TelegramAdminLink>
    {
        Task<TelegramAdminLink?> GetActiveByChatIdAsync(long chatId, CancellationToken ct = default);
        Task<TelegramAdminLink?> GetActiveByUserAsync(int applicationUserId, CancellationToken ct = default);
        Task DeactivateByChatIdAsync(long chatId, CancellationToken ct = default);
        Task DeactivateByUserAsync(int applicationUserId, CancellationToken ct = default);
    }
}

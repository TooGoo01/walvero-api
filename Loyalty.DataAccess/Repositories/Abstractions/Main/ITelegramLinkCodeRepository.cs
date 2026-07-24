using Loyalty.DataAccess.Entities.Main;

namespace Loyalty.DataAccess.Repositories.Abstractions.Main
{
    public interface ITelegramLinkCodeRepository : IGenericRepository<TelegramLinkCode>
    {
        Task<TelegramLinkCode?> GetByCodeAsync(string code, CancellationToken ct = default);
        Task DeleteByUserAsync(int applicationUserId, CancellationToken ct = default);
        Task DeleteByCodeAsync(string code, CancellationToken ct = default);
    }
}

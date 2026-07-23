using Loyalty.DataAccess.Context;
using Loyalty.DataAccess.Entities.Main;
using Loyalty.DataAccess.Repositories.Abstractions.Main;
using Microsoft.EntityFrameworkCore;

namespace Loyalty.DataAccess.Repositories.Implementations.Main
{
    public class TelegramAdminLinkRepository : GenericRepository<TelegramAdminLink>, ITelegramAdminLinkRepository
    {
        public TelegramAdminLinkRepository(LoyaltyWalletDbContext LoyaltyDbContext) : base(LoyaltyDbContext)
        {
        }

        public Task<TelegramAdminLink?> GetActiveByChatIdAsync(long chatId, CancellationToken ct = default)
            => GetAsQueryable()
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.ChatId == chatId && x.IsActive, ct);

        public Task<TelegramAdminLink?> GetActiveByUserAsync(int applicationUserId, CancellationToken ct = default)
            => GetAsQueryable()
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.ApplicationUserId == applicationUserId && x.IsActive, ct);

        // ExecuteUpdate — dərhal DB-yə yazır, UnitOfWork.SaveChanges gözləmir
        public Task DeactivateByChatIdAsync(long chatId, CancellationToken ct = default)
            => GetAsQueryable()
                .Where(x => x.ChatId == chatId && x.IsActive)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(x => x.IsActive, false)
                    .SetProperty(x => x.EditDate, DateTime.UtcNow), ct);

        public Task DeactivateByUserAsync(int applicationUserId, CancellationToken ct = default)
            => GetAsQueryable()
                .Where(x => x.ApplicationUserId == applicationUserId && x.IsActive)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(x => x.IsActive, false)
                    .SetProperty(x => x.EditDate, DateTime.UtcNow), ct);
    }
}

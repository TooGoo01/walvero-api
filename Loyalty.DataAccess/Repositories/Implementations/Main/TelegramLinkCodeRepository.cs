using Loyalty.DataAccess.Context;
using Loyalty.DataAccess.Entities.Main;
using Loyalty.DataAccess.Repositories.Abstractions.Main;
using Microsoft.EntityFrameworkCore;

namespace Loyalty.DataAccess.Repositories.Implementations.Main
{
    public class TelegramLinkCodeRepository : GenericRepository<TelegramLinkCode>, ITelegramLinkCodeRepository
    {
        public TelegramLinkCodeRepository(LoyaltyWalletDbContext LoyaltyDbContext) : base(LoyaltyDbContext)
        {
        }

        public Task<TelegramLinkCode?> GetByCodeAsync(string code, CancellationToken ct = default)
            => GetAsQueryable()
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Code == code, ct);

        // ExecuteDelete — dərhal DB-yə yazır, UnitOfWork.SaveChanges gözləmir
        public Task DeleteByUserAsync(int applicationUserId, CancellationToken ct = default)
            => GetAsQueryable()
                .Where(x => x.ApplicationUserId == applicationUserId)
                .ExecuteDeleteAsync(ct);

        public Task DeleteByCodeAsync(string code, CancellationToken ct = default)
            => GetAsQueryable()
                .Where(x => x.Code == code)
                .ExecuteDeleteAsync(ct);
    }
}

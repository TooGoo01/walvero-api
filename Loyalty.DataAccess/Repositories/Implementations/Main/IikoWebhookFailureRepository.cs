using Loyalty.DataAccess.Context;
using Loyalty.DataAccess.Entities.Main;
using Loyalty.DataAccess.Repositories.Abstractions.Main;
using Microsoft.EntityFrameworkCore;

namespace Loyalty.DataAccess.Repositories.Implementations.Main
{
    public class IikoWebhookFailureRepository : GenericRepository<IikoWebhookFailure>, IIikoWebhookFailureRepository
    {
        public IikoWebhookFailureRepository(LoyaltyWalletDbContext loyaltyDbContext) : base(loyaltyDbContext)
        {
        }

        public async Task<IikoWebhookFailure?> GetByOrderAsync(string? orderId, string? eventType, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(orderId)) return null;
            return await GetAsQueryable()
                .FirstOrDefaultAsync(x => x.OrderId == orderId && x.EventType == eventType, ct);
        }

        public async Task<List<IikoWebhookFailure>> GetPendingAsync(int maxAttempts, DateTime minRegDate, int take, CancellationToken ct = default)
        {
            return await GetAsQueryable()
                .Where(x => x.Status == 0 && x.AttemptCount < maxAttempts && x.RegDate >= minRegDate)
                .OrderBy(x => x.LastAttemptAt ?? x.RegDate)
                .Take(take)
                .ToListAsync(ct);
        }
    }
}

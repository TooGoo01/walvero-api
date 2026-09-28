using Loyalty.DataAccess.Context;
using Loyalty.DataAccess.Entities.Main;
using Loyalty.DataAccess.Enums;
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

        public async Task<List<IikoWebhookFailure>> GetDueAsync(DateTime nowUtc, DateTime minRegDate, int take, CancellationToken ct = default)
        {
            return await GetAsQueryable()
                .Where(x => x.Status == IikoWebhookFailureStatus.Pending
                            && x.RegDate >= minRegDate
                            && (x.NextAttemptAt == null || x.NextAttemptAt <= nowUtc))
                .OrderBy(x => x.NextAttemptAt ?? x.RegDate)
                .Take(take)
                .ToListAsync(ct);
        }

        public async Task<List<IikoWebhookFailure>> ListAsync(int? status, string? terminalGroupId, int take, CancellationToken ct = default)
        {
            var q = GetAsQueryable();
            if (status.HasValue) q = q.Where(x => x.Status == status.Value);
            if (!string.IsNullOrWhiteSpace(terminalGroupId)) q = q.Where(x => x.TerminalGroupId == terminalGroupId);
            return await q.OrderByDescending(x => x.Id).Take(take).ToListAsync(ct);
        }

        public async Task<List<IikoWebhookFailure>> GetByTerminalGroupAsync(string terminalGroupId, List<int> statuses, CancellationToken ct = default)
        {
            return await GetAsQueryable()
                .Where(x => x.TerminalGroupId == terminalGroupId && statuses.Contains(x.Status))
                .ToListAsync(ct);
        }
    }
}

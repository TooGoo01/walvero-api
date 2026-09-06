using Loyalty.DataAccess.Context;
using Loyalty.DataAccess.Entities.Main;
using Loyalty.DataAccess.Repositories.Abstractions.Main;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Loyalty.DataAccess.Repositories.Implementations.Main
{
    public class TransactionRepository : GenericRepository<Transaction>, ITransactionRepository
    {
        public TransactionRepository(LoyaltyWalletDbContext LoyaltyDbContext) : base(LoyaltyDbContext)
        {
        }
        public async Task<IEnumerable<Transaction>> GetByCardIdAsync(int cardId, CancellationToken cancellationToken = default)
        {
            return await GetAsQueryable()
                .Where(t => t.LoyaltyCardId == cardId && t.IsActive)
                .OrderByDescending(t => t.LocalOccurredAt)
                .ToListAsync(cancellationToken);
        }

        public async Task<IEnumerable<Transaction>> GetByCardIdWithPaginationAsync(
            int cardId,
            int pageNumber,
            int pageSize,
            CancellationToken cancellationToken = default)
        {
            return await GetAsQueryable()
                .Where(t => t.LoyaltyCardId == cardId && t.IsActive)
                .OrderByDescending(t => t.LocalOccurredAt)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(cancellationToken);
        }

        public async Task<IEnumerable<Transaction>> GetByCustomerIdAsync(int customerId, CancellationToken cancellationToken = default)
        {
            return await GetAsQueryable()
                .Include(t => t.LoyaltyCard)
                    .ThenInclude(lc => lc.Customer)
                .Where(t => t.LoyaltyCard.CustomerId == customerId && t.IsActive)
                .OrderByDescending(t => t.RegDate)
                .ToListAsync(cancellationToken);
        }

        public async Task<IEnumerable<Transaction>> GetByApplicationUserIdAsync(int applicationUserId, CancellationToken cancellationToken = default)
        {
            return await GetAsQueryable()
                .Include(t => t.LoyaltyCard)
                    .ThenInclude(lc => lc.Customer)
                .Include(t => t.LoyaltyCard)
                    .ThenInclude(lc => lc.Tenant) // mobil tarixçədə tenant adı üçün
                .Include(t => t.Branch) // filial adı üçün
                .Where(t => t.IsActive
                         && t.LoyaltyCard.Customer != null
                         && t.LoyaltyCard.Customer.applicationUserId == applicationUserId)
                .OrderByDescending(t => t.RegDate)
                .ToListAsync(cancellationToken);
        }

        public async Task<Dictionary<int, DateTime>> GetLastActivityByCardIdsAsync(IEnumerable<int> cardIds, CancellationToken cancellationToken = default)
        {
            var ids = cardIds?.Distinct().ToList() ?? new List<int>();
            if (ids.Count == 0)
                return new Dictionary<int, DateTime>();

            return await GetAsQueryable()
                .Where(t => ids.Contains(t.LoyaltyCardId) && t.IsActive)
                .GroupBy(t => t.LoyaltyCardId)
                .Select(g => new { CardId = g.Key, Last = g.Max(x => x.RegDate) })
                .ToDictionaryAsync(x => x.CardId, x => x.Last, cancellationToken);
        }

        public async Task<IEnumerable<Transaction>> GetByTenantIdAsync(int tenantId, int limit, CancellationToken cancellationToken = default)
        {
            return await GetAsQueryable()
                .Include(t => t.LoyaltyCard)
                    .ThenInclude(lc => lc.Customer)
                .Where(t => t.LoyaltyCard.TenantId == tenantId && t.IsActive)
                .OrderByDescending(t => t.RegDate)
                .Take(limit)
                .ToListAsync(cancellationToken);
        }
        public async Task<IEnumerable<Transaction>> GetByTenantIdDateAsync(int tenantId, DateOnly startDate, DateOnly endDate, CancellationToken cancellationToken = default)
        {
            // Convert DateOnly to DateTimeOffset for comparison with LocalOccurredAt
            // Use UTC offset for the comparison boundaries
            var startDateTime = new DateTimeOffset(startDate.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero); // 00:00:00
            var endDateTime = new DateTimeOffset(endDate.ToDateTime(TimeOnly.MaxValue), TimeSpan.Zero);   // 23:59:59.9999999

            return await GetAsQueryable()
                .Include(t => t.LoyaltyCard)
                    .ThenInclude(lc => lc.Customer)
                .Include(t => t.Branch)
                .Where(t =>
                    t.LoyaltyCard.TenantId == tenantId &&
                    t.IsActive &&
                    t.LocalOccurredAt.Date >= startDate.ToDateTime(TimeOnly.MinValue) &&
                    t.LocalOccurredAt.Date <= endDate.ToDateTime(TimeOnly.MaxValue))
                .OrderByDescending(t => t.LocalOccurredAt)
                .ToListAsync(cancellationToken);
        }

        public async Task<decimal> GetTotalPointsEarnedAsync(int cardId, CancellationToken cancellationToken = default)
        {
            return await GetAsQueryable()
                .Where(t => t.LoyaltyCardId == cardId && t.IsActive && t.Type == TransactionType.Earned)
                .SumAsync(t => t.Points, cancellationToken);
        }

        public async Task<decimal> GetTotalPointsRedeemedAsync(int cardId, CancellationToken cancellationToken = default)
        {
            return await GetAsQueryable()
                .Where(t => t.LoyaltyCardId == cardId && t.IsActive && t.Type == TransactionType.Redeemed)
                .SumAsync(t => t.Points, cancellationToken);
        }

        public async Task<List<Transaction>> GetRecentRedeemedByCustomerAsync(int customerId, int take, CancellationToken cancellationToken = default)
        {
            return await GetAsQueryable()
                .AsNoTracking()
                .Include(t => t.Branch)
                .Where(t => t.LoyaltyCard.CustomerId == customerId
                         && t.IsActive
                         && t.Type == TransactionType.Redeemed
                         && t.Status == Enums.RedeemStatus.Applied)
                .OrderByDescending(t => t.LocalOccurredAt)
                .Take(take)
                .ToListAsync(cancellationToken);
        }
    }
}

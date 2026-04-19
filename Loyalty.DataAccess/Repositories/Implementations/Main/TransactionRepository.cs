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
                .Where(t => t.LoyaltyCardId == cardId)
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
                .Where(t => t.LoyaltyCardId == cardId)
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
                .Where(t => t.LoyaltyCard.CustomerId == customerId)
                .OrderByDescending(t => t.RegDate)
                .ToListAsync(cancellationToken);
        }

        public async Task<IEnumerable<Transaction>> GetByTenantIdAsync(int tenantId, int limit, CancellationToken cancellationToken = default)
        {
            return await GetAsQueryable()
                .Include(t => t.LoyaltyCard)
                    .ThenInclude(lc => lc.Customer)
                .Where(t => t.LoyaltyCard.TenantId == tenantId)
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
                .Where(t =>
                    t.LoyaltyCard.TenantId == tenantId &&
                    t.LocalOccurredAt.Date >= startDate.ToDateTime(TimeOnly.MinValue) &&
                    t.LocalOccurredAt.Date <= endDate.ToDateTime(TimeOnly.MaxValue))
                .OrderByDescending(t => t.LocalOccurredAt)
                .ToListAsync(cancellationToken);
        }

        public async Task<decimal> GetTotalPointsEarnedAsync(int cardId, CancellationToken cancellationToken = default)
        {
            return await GetAsQueryable()
                .Where(t => t.LoyaltyCardId == cardId && t.Type == TransactionType.Earned)
                .SumAsync(t => t.Points, cancellationToken);
        }

        public async Task<decimal> GetTotalPointsRedeemedAsync(int cardId, CancellationToken cancellationToken = default)
        {
            return await GetAsQueryable()
                .Where(t => t.LoyaltyCardId == cardId && t.Type == TransactionType.Redeemed)
                .SumAsync(t => t.Points, cancellationToken);
        }
    }
}

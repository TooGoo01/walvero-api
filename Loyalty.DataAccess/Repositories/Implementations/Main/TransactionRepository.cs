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

        public async Task<int> GetTotalPointsEarnedAsync(int cardId, CancellationToken cancellationToken = default)
        {
            return await GetAsQueryable()
                .Where(t => t.LoyaltyCardId == cardId && t.Type == TransactionType.Earned)
                .SumAsync(t => t.Points, cancellationToken);
        }

        public async Task<int> GetTotalPointsRedeemedAsync(int cardId, CancellationToken cancellationToken = default)
        {
            return await GetAsQueryable()
                .Where(t => t.LoyaltyCardId == cardId && t.Type == TransactionType.Redeemed)
                .SumAsync(t => t.Points, cancellationToken);
        }
    }
}

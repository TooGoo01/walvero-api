using Loyalty.DataAccess.Entities.Main;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Loyalty.DataAccess.Repositories.Abstractions.Main
{
    public interface ITransactionRepository : IGenericRepository<Transaction>
    {
        Task<IEnumerable<Transaction>> GetByCardIdAsync(int cardId, CancellationToken cancellationToken = default);
        Task<IEnumerable<Transaction>> GetByCardIdWithPaginationAsync(
            int cardId,
            int pageNumber,
            int pageSize,
            CancellationToken cancellationToken = default);
        Task<IEnumerable<Transaction>> GetByCustomerIdAsync(int customerId, CancellationToken cancellationToken = default);
        Task<IEnumerable<Transaction>> GetByTenantIdAsync(int tenantId, int limit, CancellationToken cancellationToken = default);
        Task<IEnumerable<Transaction>> GetByTenantIdDateAsync(int tenantId, DateOnly startDate, DateOnly endDate, CancellationToken cancellationToken = default);
        Task<int> GetTotalPointsEarnedAsync(int cardId, CancellationToken cancellationToken = default);
        Task<int> GetTotalPointsRedeemedAsync(int cardId, CancellationToken cancellationToken = default);
    }
}

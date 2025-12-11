using Loyalty.DataAccess.Entities.Main;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Loyalty.DataAccess.Repositories.Abstractions.Main
{
    public interface ILoyaltyCardRepository:IGenericRepository<LoyaltyCard>
    {
        Task<LoyaltyCard?> GetByCardNumberAsync(int tenantId, string cardNumber, CancellationToken ct = default);
        Task<LoyaltyCard?> GetByBarcodeValueAsync(int tenantId, string cardNumber, CancellationToken ct = default);
        Task<bool> ExistsAsync(int tenantId, string cardNumber, CancellationToken ct = default);
        Task<List<LoyaltyCard>> ListByCustomerAsync(int tenantId, int customerId, CancellationToken ct = default);
        Task<LoyaltyCard?> GetLastActiveByCustomerAsync(int tenantId, int programId, int customerId, CancellationToken ct);
        Task<LoyaltyCard?> GetById(int id,CancellationToken ct);
        Task<List<LoyaltyCard>> GetLastActiveCardsForApplicationUserAsync(
    int applicationUserId,
    string? name, int? categoryId,
    CancellationToken ct = default);
    }
}

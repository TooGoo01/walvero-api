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
        Task<LoyaltyCard?> GetLastInactiveByCustomerAsync(int tenantId, int programId, int customerId, CancellationToken ct);
        Task<LoyaltyCard?> GetById(int id,CancellationToken ct);
        Task<List<LoyaltyCard>> GetLastActiveCardsForApplicationUserAsync(
    int applicationUserId,
    string? name, int? categoryId,
    CancellationToken ct = default);
        // OPTIMIZATION: Get count without loading all data into memory
        Task<int> GetActiveCardsCountForApplicationUserAsync(
            int applicationUserId,
            string? name,
            int? categoryId,
            int? countryId,
            int? cityId,
            CancellationToken ct = default);
        // OPTIMIZATION: Get paginated cards directly from database
        Task<List<LoyaltyCard>> GetLastActiveCardsForApplicationUserPaginatedAsync(
            int applicationUserId,
            string? name,
            int? categoryId,
            int? countryId,
            int? cityId,
            int currentPage,
            int pageSize,
            CancellationToken ct = default);

        /// <summary>
        /// Get a card by ID with full details for wallet card details view.
        /// Includes: Customer, LoyaltyProgram, CardTemplate, Tenant (with Branding and Category).
        /// </summary>
        Task<LoyaltyCard?> GetCardWithFullDetailsAsync(int cardId, CancellationToken ct = default);

        /// <summary>
        /// Check if a card belongs to a specific application user (for authorization).
        /// </summary>
        Task<bool> CardBelongsToUserAsync(int cardId, int applicationUserId, CancellationToken ct = default);
    }
}

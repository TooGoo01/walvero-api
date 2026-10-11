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
        /// <summary>
        /// Kassirin yazdığı qısa kod (barkodun son N rəqəmi) ilə aktiv kart. Matches = tenant daxilində uyğun kart sayı;
        /// 1 → Card dolu (tracked, Customer+applicationUser ilə), 0 və ya 2+ → Card null (ambiguous).
        /// </summary>
        Task<(LoyaltyCard? Card, int Matches)> GetByBarcodeSuffixAsync(int tenantId, string suffix, CancellationToken ct = default);
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
        /// Count + paginated data in single method to reduce DB roundtrips.
        /// </summary>
        Task<(int totalCount, List<LoyaltyCard> cards)> GetMyCardsPaginatedAsync(
            int applicationUserId,
            string? name,
            int? categoryId,
            int? countryId,
            int? cityId,
            int currentPage,
            int pageSize,
            CancellationToken ct = default);

        /// <summary>User-in aktiv kartlarının distinct tenant Id-ləri (geofence üçün).</summary>
        Task<List<int>> GetActiveCardTenantIdsAsync(int applicationUserId, CancellationToken ct = default);

        /// <summary>Bütün aktiv kartlar (səhifələmə YOX) — yaxınlıq server-side sortu üçün. Branches daxil.</summary>
        Task<List<LoyaltyCard>> GetAllMyCardsWithBranchesAsync(int applicationUserId, string? name, int? categoryId, int? countryId, int? cityId, CancellationToken ct = default);

        /// <summary>
        /// Get a card by ID with full details for wallet card details view.
        /// Includes: Customer, LoyaltyProgram, CardTemplate, Tenant (with Branding and Category).
        /// </summary>
        Task<LoyaltyCard?> GetCardWithFullDetailsAsync(int cardId, CancellationToken ct = default);

        /// <summary>
        /// Get card with full details + ownership check in single query.
        /// Returns null if card doesn't exist or doesn't belong to user.
        /// </summary>
        Task<LoyaltyCard?> GetCardWithOwnerCheckAsync(int cardId, int applicationUserId, CancellationToken ct = default);

        /// <summary>
        /// Check if a card belongs to a specific application user (for authorization).
        /// </summary>
        Task<bool> CardBelongsToUserAsync(int cardId, int applicationUserId, CancellationToken ct = default);

        /// <summary>
        /// Find the user's active card for a given tenant — used by the public
        /// wallet-info endpoint to upgrade response from TenantOnly → CardDetails
        /// when JWT is present and user has a card for that tenant.
        /// Eager-loads same relations as GetCardWithOwnerCheckAsync for full detail render.
        /// </summary>
        Task<LoyaltyCard?> GetActiveCardForTenantAndUserAsync(int tenantId, int applicationUserId, CancellationToken ct = default);
    }
}

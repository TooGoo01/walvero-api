using Loyalty.Business.Dtos.Get.Main;
using Loyalty.Business.Results;

namespace Loyalty.Business.Services.Abstractions.Main
{
    /// <summary>
    /// Service for retrieving wallet card details with proper authorization.
    /// Provides two separate operations:
    /// 1. GetTenantWalletInfoAsync: Returns only public tenant info (no auth required)
    /// 2. GetCardDetailsAsync: Returns full card details with authorization checks
    /// </summary>
    public interface IWalletCardDetailsService
    {
        /// <summary>
        /// Get wallet info for a tenant.
        /// - Anonymous (applicationUserId == null): returns TenantOnly mode (public info).
        /// - Authenticated (applicationUserId != null): if the user has an active card for this tenant,
        ///   automatically upgrades response to CardDetails mode (full balance + customer data).
        ///   If no card found, falls back to TenantOnly.
        /// This eliminates frontend race conditions where cardId is not yet known.
        /// </summary>
        /// <param name="tenantId">Required: The tenant ID</param>
        /// <param name="applicationUserId">Optional: ApplicationUser id from JWT claims; null for anonymous</param>
        /// <param name="ct">Cancellation token</param>
        Task<ServiceResult<WalletCardDetailsResponseDto>> GetTenantWalletInfoAsync(
            int tenantId,
            int? applicationUserId,
            CancellationToken ct = default);

        /// <summary>
        /// Get wallet card details for a specific card.
        /// Returns full card details including customer-specific information.
        /// Requires authorization - user must own the card.
        /// </summary>
        /// <param name="tenantId">Required: The tenant ID</param>
        /// <param name="cardId">Required: The card ID</param>
        /// <param name="ct">Cancellation token</param>
        /// <returns>
        /// Success with WalletCardDetailsResponseDto (Mode = CardDetails):
        /// - Mode = CardDetails when authorized
        /// 
        /// Failure scenarios:
        /// - Card not found (404)
        /// - Card belongs to different tenant (404 - don't leak existence)
        /// - User not authorized to view card (404 - don't leak existence)
        /// - No authenticated user (401)
        /// </returns>
        Task<ServiceResult<WalletCardDetailsResponseDto>> GetCardDetailsAsync(
            int tenantId,
            int cardId,
            CancellationToken ct = default);
    }
}

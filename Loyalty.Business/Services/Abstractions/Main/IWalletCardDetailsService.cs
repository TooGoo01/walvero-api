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
        /// Get public tenant wallet information.
        /// Returns ONLY tenant-level public info (branding, colors, descriptions).
        /// NO customer-specific data is returned (no balance, points, card numbers, etc.).
        /// </summary>
        /// <param name="tenantId">Required: The tenant ID</param>
        /// <param name="ct">Cancellation token</param>
        /// <returns>
        /// Success with WalletCardDetailsResponseDto (Mode = TenantOnly, Card = null)
        /// Failure if tenant not found
        /// </returns>
        Task<ServiceResult<WalletCardDetailsResponseDto>> GetTenantWalletInfoAsync(
            int tenantId,
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

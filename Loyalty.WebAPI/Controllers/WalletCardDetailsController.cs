using Loyalty.Business.Dtos.Get.Main;
using Loyalty.Business.Results;
using Loyalty.Business.Services.Abstractions.Main;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Net;

namespace Loyalty.WebAPI.Controllers
{
    /// <summary>
    /// Controller for wallet card details (Flutter WalletCardDetailsView).
    /// Provides two separate endpoints:
    /// - GET /api/tenants/{tenantId}/wallet-info: Public tenant info (no auth required)
    /// - GET /api/tenants/{tenantId}/cards/{cardId}/details: Card details (auth required)
    /// </summary>
    [Route("api/tenants/{tenantId}")]
    [ApiController]
    public class WalletCardDetailsController : ControllerBase
    {
        private readonly IWalletCardDetailsService _walletCardDetailsService;

        public WalletCardDetailsController(IWalletCardDetailsService walletCardDetailsService)
        {
            _walletCardDetailsService = walletCardDetailsService;
        }

        /// <summary>
        /// Get public tenant wallet information.
        /// This endpoint returns ONLY tenant-level public info (branding, colors, logo, descriptions).
        /// No authentication required. No customer-specific data is returned.
        /// </summary>
        /// <param name="tenantId">Required: The tenant ID</param>
        /// <param name="ct">Cancellation token</param>
        /// <returns>WalletCardDetailsResponseDto with Mode = TenantOnly</returns>
        /// <response code="200">Success - returns tenant public info</response>
        /// <response code="400">Bad request - invalid tenantId</response>
        /// <response code="404">Not found - tenant not found</response>
        [HttpGet("wallet-info")]
        [AllowAnonymous]
        [ProducesResponseType(typeof(ServiceResult<WalletCardDetailsResponseDto>), (int)HttpStatusCode.OK)]
        [ProducesResponseType(typeof(ServiceResult<WalletCardDetailsResponseDto>), (int)HttpStatusCode.NotFound)]
        [ProducesResponseType(typeof(ServiceResult<WalletCardDetailsResponseDto>), (int)HttpStatusCode.BadRequest)]
        public async Task<ActionResult<ServiceResult<WalletCardDetailsResponseDto>>> GetTenantWalletInfo(
            [FromRoute] int tenantId,
            CancellationToken ct)
        {
            if (tenantId <= 0)
            {
                return BadRequest(ServiceResult<WalletCardDetailsResponseDto>.Fail("TenantId must be greater than 0"));
            }

            var result = await _walletCardDetailsService.GetTenantWalletInfoAsync(tenantId, ct);

            if (!result.Success)
            {
                if (result.Message.Contains("not found", StringComparison.OrdinalIgnoreCase))
                {
                    return NotFound(result);
                }

                return BadRequest(result);
            }

            return Ok(result);
        }

        /// <summary>
        /// Get wallet card details for a specific card.
        /// This endpoint returns full card details including customer-specific information.
        /// Authentication is required. User must own the card to access it.
        /// </summary>
        /// <param name="tenantId">Required: The tenant ID</param>
        /// <param name="cardId">Required: The card ID</param>
        /// <param name="ct">Cancellation token</param>
        /// <returns>WalletCardDetailsResponseDto with Mode = CardDetails</returns>
        /// <response code="200">Success - returns card details</response>
        /// <response code="400">Bad request - invalid parameters</response>
        /// <response code="401">Unauthorized - authentication required</response>
        /// <response code="404">Not found - card not found or access denied</response>
        [HttpGet("cards/{cardId:int}/details")]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Policy = "CustomerOnly")]
        [ProducesResponseType(typeof(ServiceResult<WalletCardDetailsResponseDto>), (int)HttpStatusCode.OK)]
        [ProducesResponseType(typeof(ServiceResult<WalletCardDetailsResponseDto>), (int)HttpStatusCode.NotFound)]
        [ProducesResponseType(typeof(ServiceResult<WalletCardDetailsResponseDto>), (int)HttpStatusCode.Unauthorized)]
        [ProducesResponseType(typeof(ServiceResult<WalletCardDetailsResponseDto>), (int)HttpStatusCode.BadRequest)]
        public async Task<ActionResult<ServiceResult<WalletCardDetailsResponseDto>>> GetCardDetails(
            [FromRoute] int tenantId,
            [FromRoute] int cardId,
            CancellationToken ct)
        {
            if (tenantId <= 0)
            {
                return BadRequest(ServiceResult<WalletCardDetailsResponseDto>.Fail("TenantId must be greater than 0"));
            }

            if (cardId <= 0)
            {
                return BadRequest(ServiceResult<WalletCardDetailsResponseDto>.Fail("CardId must be greater than 0"));
            }

            var result = await _walletCardDetailsService.GetCardDetailsAsync(tenantId, cardId, ct);

            if (!result.Success)
            {
                if (result.Message.Contains("not found", StringComparison.OrdinalIgnoreCase))
                {
                    return NotFound(result);
                }

                if (result.Message.Contains("Authentication required", StringComparison.OrdinalIgnoreCase))
                {
                    return Unauthorized(result);
                }

                return BadRequest(result);
            }

            return Ok(result);
        }
    }
}

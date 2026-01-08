using Loyalty.Business.Dtos.Post.Main;
using Loyalty.Business.Results;
using Loyalty.Business.Services.Abstractions.Main;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Net;

namespace Loyalty.WebAPI.Controllers
{
    /// <summary>
    /// Controller for creating wallet passes for Walvero shadow tenant.
    /// These passes are based on user's WalveroId, not loyalty cards.
    /// </summary>
    [Route("api/wallet/passes/walvero")]
    [ApiController]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public class WalveroWalletPassController : ControllerBase
    {
        private readonly IWalveroWalletPassService _walveroWalletPassService;
        private readonly ITenantProvider _tenantProvider;

        public WalveroWalletPassController(
            IWalveroWalletPassService walveroWalletPassService,
            ITenantProvider tenantProvider)
        {
            _walveroWalletPassService = walveroWalletPassService;
            _tenantProvider = tenantProvider;
        }

        /// <summary>
        /// Create or retrieve a wallet pass for a user's WalveroId.
        /// </summary>
        /// <param name="request">Request with userId and platform</param>
        /// <param name="ct">Cancellation token</param>
        /// <returns>Pass URLs and metadata</returns>
        /// <response code="200">Success - pass created or retrieved</response>
        /// <response code="400">Bad request - invalid parameters or missing walveroId</response>
        /// <response code="403">Forbidden - user cannot create pass for another user</response>
        /// <response code="500">Server error - Walvero tenant misconfiguration</response>
        [HttpPost("create")]
        [ProducesResponseType(typeof(ServiceResult<WalveroWalletPassResponseDto>), (int)HttpStatusCode.OK)]
        [ProducesResponseType(typeof(ServiceResult<WalveroWalletPassResponseDto>), (int)HttpStatusCode.BadRequest)]
        [ProducesResponseType(typeof(ServiceResult<WalveroWalletPassResponseDto>), (int)HttpStatusCode.Forbidden)]
        [ProducesResponseType(typeof(ServiceResult<WalveroWalletPassResponseDto>), (int)HttpStatusCode.InternalServerError)]
        public async Task<ActionResult<ServiceResult<WalveroWalletPassResponseDto>>> CreateWalveroPass(
            [FromBody] CreateWalveroPassRequestDto request,
            CancellationToken ct)
        {
            // Validate request
            if (request.UserId <= 0)
            {
                return BadRequest(ServiceResult<WalveroWalletPassResponseDto>.Fail("UserId must be greater than 0"));
            }

            if (string.IsNullOrWhiteSpace(request.Platform))
            {
                return BadRequest(ServiceResult<WalveroWalletPassResponseDto>.Fail("Platform is required"));
            }

            // Authorization check - user can only create pass for themselves (unless admin)
            var currentUserId = _tenantProvider.CurrentUserId;
            if (string.IsNullOrEmpty(currentUserId))
            {
                return Unauthorized(ServiceResult<WalveroWalletPassResponseDto>.Fail("Authentication required"));
            }

            if (!int.TryParse(currentUserId, out var authenticatedUserId))
            {
                return Unauthorized(ServiceResult<WalveroWalletPassResponseDto>.Fail("Invalid user ID"));
            }

            // Check if user is trying to create pass for themselves or is admin
            // TODO: Add admin role check if needed
            if (authenticatedUserId != request.UserId)
            {
                return StatusCode(
                    (int)HttpStatusCode.Forbidden,
                    ServiceResult<WalveroWalletPassResponseDto>.Fail("You can only create passes for your own account"));
            }

            // Call service
            var result = await _walveroWalletPassService.CreateWalveroPassAsync(
                request.UserId,
                request.Platform,
                ct);

            if (!result.Success)
            {
                if (result.Message.Contains("walveroId", StringComparison.OrdinalIgnoreCase))
                {
                    return BadRequest(result);
                }

                if (result.Message.Contains("shadow tenant missing", StringComparison.OrdinalIgnoreCase))
                {
                    return StatusCode((int)HttpStatusCode.InternalServerError, result);
                }

                return BadRequest(result);
            }

            return Ok(result);
        }
    }
}


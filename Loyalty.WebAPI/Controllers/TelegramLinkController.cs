using System.Security.Claims;
using Loyalty.Business.Results;
using Loyalty.Business.Services.Abstractions.Main;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Loyalty.WebAPI.Controllers
{
    [ApiController]
    [Route("api/telegram")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Policy = "TenantAdminOrModerator")]
    public class TelegramLinkController : ControllerBase
    {
        private readonly ITelegramBotService _botService;
        private readonly ITenantProvider _tenantProvider;

        public TelegramLinkController(ITelegramBotService botService, ITenantProvider tenantProvider)
        {
            _botService = botService;
            _tenantProvider = tenantProvider;
        }

        [HttpPost("link-code")]
        public async Task<IActionResult> GenerateLinkCode(CancellationToken ct)
        {
            var tenantId = _tenantProvider.CurrentTenantId;
            if (!tenantId.HasValue)
                return BadRequest(new ServiceResult(false, "Tenant not found."));

            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdStr, out var userId))
                return Unauthorized();

            var result = await _botService.GenerateLinkCodeAsync(userId, tenantId.Value, ct);
            return Ok(new ServiceResult(true, result, "Link code generated"));
        }

        [HttpGet("link-status")]
        public async Task<IActionResult> LinkStatus(CancellationToken ct)
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdStr, out var userId))
                return Unauthorized();

            var linked = await _botService.HasActiveLinkAsync(userId, ct);
            return Ok(new ServiceResult(true, new { linked }, ""));
        }

        [HttpDelete("link")]
        public async Task<IActionResult> Unlink(CancellationToken ct)
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdStr, out var userId))
                return Unauthorized();

            await _botService.UnlinkUserAsync(userId, ct);
            return Ok(new ServiceResult(true, "Telegram link removed"));
        }
    }
}

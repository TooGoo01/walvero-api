using Loyalty.Business.Dtos.Post.Main;
using Loyalty.Business.Results;
using Loyalty.Business.Services.Abstractions.Main;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Loyalty.WebAPI.Controllers
{
    [Route("api/[controller]/[action]")]
    [ApiController]
    public class AnalyticsController : ControllerBase
    {
        private readonly IAnalyticsService _analyticsService;
        private readonly ITenantProvider _tenantProvider;

        public AnalyticsController(IAnalyticsService analyticsService, ITenantProvider tenantProvider)
        {
            _analyticsService = analyticsService;
            _tenantProvider = tenantProvider;
        }

        /// <summary>Mobil app-dan link keçidini qeyd edir (anonim, guest + login).</summary>
        [HttpPost]
        [AllowAnonymous]
        public async Task<IActionResult> Track([FromBody] TrackLinkClickDto dto)
        {
            // Fire-and-forget: uğursuzluqda belə app-ı bloklamırıq.
            try
            {
                await _analyticsService.TrackLinkClickAsync(dto);
            }
            catch
            {
                // ignore — analitika kritik deyil
            }
            return Ok(new ServiceResult(true));
        }

        /// <summary>Tenant üçün link-klik statistikası (aggregate).</summary>
        [HttpGet]
        [Authorize(
            AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme,
            Policy = "TenantAdminOrModerator")]
        public async Task<IActionResult> LinkClicks([FromQuery] string? startDate, [FromQuery] string? endDate)
        {
            var tenantId = _tenantProvider.CurrentTenantId ?? 0;
            if (tenantId <= 0)
                return Ok(new ServiceResult(false, "TenantId is required"));

            var today = DateOnly.FromDateTime(DateTime.Today);
            var start = !string.IsNullOrWhiteSpace(startDate) && DateOnly.TryParse(startDate, out var sd)
                ? sd
                : today.AddDays(-30);
            var end = !string.IsNullOrWhiteSpace(endDate) && DateOnly.TryParse(endDate, out var ed)
                ? ed
                : today;

            var result = await _analyticsService.GetLinkClickStatsAsync(tenantId, start, end);
            return Ok(result);
        }
    }
}

using Loyalty.Business.Dtos.Get.Main;
using Loyalty.Business.Results;
using Loyalty.Business.Services.Abstractions.Main;
using Loyalty.DataAccess.Entities.Main;
using Loyalty.DataAccess.Enums;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Net;

namespace Loyalty.WebAPI.Controllers
{
    [Route("api/[controller]/[action]")]
    [ApiController]
    [Authorize(
        AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme,
        Policy = "TenantAdminOrModerator")]
    public class StatisticsController : ControllerBase
    {
        private readonly ITransactionService _transactionService;
        private readonly ITenantProvider _tenantProvider;
        private readonly IStatisticsService _statisticsService;

        public StatisticsController(
            ITransactionService transactionService,
            ITenantProvider tenantProvider,
            IStatisticsService statisticsService)
        {
            _transactionService = transactionService;
            _tenantProvider = tenantProvider;
            _statisticsService = statisticsService;
        }

        /// <summary>
        /// Dashboard v2: SQL-side aggregatlar (yalnız Applied tx-lər). groupBy: day|week|month —
        /// Series bucket ölçüsü. Previous (delta %), yeni müştəri, kart platforma bölgüsü,
        /// filial breakdown daxildir. Köhnə dailyStats sahəsi geri uyğunluq üçün qalır.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> Dashboard(
            [FromQuery] string? startDate,
            [FromQuery] string? endDate,
            [FromQuery] string? groupBy)
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

            var g = (groupBy ?? "day").Trim().ToLowerInvariant();
            if (g != "week" && g != "month") g = "day";

            var result = await _statisticsService.GetDashboardAsync(tenantId, start, end, null, g);
            if (!result.Success || result.Data is null)
                return Ok(new ServiceResult(false, result.Message ?? "No data"));

            return Ok(new ServiceResult(true, result.Data));
        }
    }
}

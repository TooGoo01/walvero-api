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

        [HttpGet]
        public async Task<IActionResult> Dashboard([FromQuery] string? startDate, [FromQuery] string? endDate)
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

            var result = await _transactionService.GetByTenantIdAndDate(tenantId, start, end);

            if (!result.Success || result.Data is not List<GetTransactionDto> txList)
                return Ok(new ServiceResult(false, result.Message ?? "No data"));

            var totalPointsEarned = 0;
            var totalPointsSpent = 0;

            var dailyMap = new SortedDictionary<string, (int count, int earned, int spent)>();

            // Yalnız tam tətbiq olunmuş tx-lər KPI-ya daxil olur. PendingOcr / FlaggedForReview /
            // Pending / Rejected / Expired / Reversed kart balansına yazılmayıb — "Points Given"
            // statistikasında onları saymaq səhv informasiya verir.
            var appliedTxs = txList.Where(t => t.Status == RedeemStatus.Applied).ToList();
            var totalAppliedCount = appliedTxs.Count;

            foreach (var tx in appliedTxs)
            {
                var points = tx.Points;
                var type = tx.Type;
                var dateKey = tx.LocalOccurredAt.ToString("yyyy-MM-dd");

                if (type == TransactionType.Earned || type == TransactionType.Bonus)
                    totalPointsEarned += points;
                else if (type == TransactionType.Redeemed)
                    totalPointsSpent += points;

                if (!dailyMap.ContainsKey(dateKey))
                    dailyMap[dateKey] = (0, 0, 0);

                var d = dailyMap[dateKey];
                dailyMap[dateKey] = (
                    d.count + 1,
                    d.earned + (type == TransactionType.Earned || type == TransactionType.Bonus ? points : 0),
                    d.spent + (type == TransactionType.Redeemed ? points : 0)
                );
            }

            var dailyStats = dailyMap.Select(kvp => new
            {
                date = kvp.Key,
                transactionCount = kvp.Value.count,
                pointsEarned = kvp.Value.earned,
                pointsSpent = kvp.Value.spent
            }).ToList();

            // Ümumi müştəri = tenant-ın aktiv kartlı unikal müştəriləri. Əvvəlki kod tarix
            // aralığında Applied tx-i olan KART sayını göstərirdi — həm kartı müştəri kimi
            // sayırdı, həm də əməliyyatsız müştəriləri itirirdi.
            var uniqueCustomers = await _statisticsService.GetUniqueCustomerCountAsync(tenantId);

            var dashboard = new
            {
                totalTransactions = totalAppliedCount,
                totalPointsEarned,
                totalPointsSpent,
                uniqueCustomers,
                dailyStats
            };

            return Ok(new ServiceResult(true, dashboard));
        }
    }
}

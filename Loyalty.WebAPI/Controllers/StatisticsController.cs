using Loyalty.Business.Results;
using Loyalty.Business.Services.Abstractions.Main;
using Loyalty.DataAccess.Entities.Main;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

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

        public StatisticsController(ITransactionService transactionService, ITenantProvider tenantProvider)
        {
            _transactionService = transactionService;
            _tenantProvider = tenantProvider;
        }

        [HttpGet]
        public async Task<IActionResult> Dashboard([FromQuery] string? startDate, [FromQuery] string? endDate)
        {
            var tenantId = _tenantProvider.CurrentTenantId ?? 0;
            if (tenantId <= 0)
                return Ok(ServiceResult.Fail("TenantId is required"));

            var today = DateOnly.FromDateTime(DateTime.Today);
            var start = !string.IsNullOrWhiteSpace(startDate) && DateOnly.TryParse(startDate, out var sd)
                ? sd
                : today.AddDays(-30);
            var end = !string.IsNullOrWhiteSpace(endDate) && DateOnly.TryParse(endDate, out var ed)
                ? ed
                : today;

            var result = await _transactionService.GetByTenantIdAndDate(tenantId, start, end);

            if (!result.Success || result.Data is not IEnumerable<object> transactions)
                return Ok(ServiceResult.Fail(result.Message ?? "No data"));

            // Cast to dynamic to access properties
            var txList = ((IEnumerable<dynamic>)result.Data).ToList();

            var totalTransactions = txList.Count;
            var totalPointsEarned = 0;
            var totalPointsSpent = 0;
            var customerIds = new HashSet<int>();

            var dailyMap = new SortedDictionary<string, (int count, int earned, int spent)>();

            foreach (var tx in txList)
            {
                int points = (int)(tx.Points ?? 0);
                int type = (int)(tx.Type ?? 0);
                string dateKey = tx.LocalOccurredAt != null
                    ? ((DateTimeOffset)tx.LocalOccurredAt).ToString("yyyy-MM-dd")
                    : "unknown";

                if (type == (int)TransactionType.Earned || type == (int)TransactionType.Bonus)
                    totalPointsEarned += points;
                else if (type == (int)TransactionType.Redeemed)
                    totalPointsSpent += points;

                if (tx.LoyaltyCardId != null && tx.LoyaltyCardId > 0)
                    customerIds.Add((int)tx.LoyaltyCardId);

                if (!dailyMap.ContainsKey(dateKey))
                    dailyMap[dateKey] = (0, 0, 0);

                var d = dailyMap[dateKey];
                dailyMap[dateKey] = (
                    d.count + 1,
                    d.earned + (type == (int)TransactionType.Earned || type == (int)TransactionType.Bonus ? points : 0),
                    d.spent + (type == (int)TransactionType.Redeemed ? points : 0)
                );
            }

            var dailyStats = dailyMap.Select(kvp => new
            {
                date = kvp.Key,
                transactionCount = kvp.Value.count,
                pointsEarned = kvp.Value.earned,
                pointsSpent = kvp.Value.spent
            }).ToList();

            var dashboard = new
            {
                totalTransactions,
                totalPointsEarned,
                totalPointsSpent,
                uniqueCustomers = customerIds.Count,
                dailyStats
            };

            return Ok(new ServiceResult(true, dashboard));
        }
    }
}

using Loyalty.Business.Dtos.Get.Main;
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

            var totalTransactions = txList.Count;
            var totalPointsEarned = 0;
            var totalPointsSpent = 0;
            var cardIds = new HashSet<int>();

            var dailyMap = new SortedDictionary<string, (int count, int earned, int spent)>();

            foreach (var tx in txList)
            {
                var points = tx.Points;
                var type = tx.Type;
                var dateKey = tx.LocalOccurredAt.ToString("yyyy-MM-dd");

                if (type == TransactionType.Earned || type == TransactionType.Bonus)
                    totalPointsEarned += points;
                else if (type == TransactionType.Redeemed)
                    totalPointsSpent += points;

                if (tx.LoyaltyCardId > 0)
                    cardIds.Add(tx.LoyaltyCardId);

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

            var dashboard = new
            {
                totalTransactions,
                totalPointsEarned,
                totalPointsSpent,
                uniqueCustomers = cardIds.Count,
                dailyStats
            };

            return Ok(new ServiceResult(true, dashboard));
        }
    }
}

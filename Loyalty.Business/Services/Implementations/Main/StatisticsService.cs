using Loyalty.Business.Dtos.Get.Main;
using Loyalty.Business.Results;
using Loyalty.Business.Services.Abstractions.Main;
using Loyalty.DataAccess.Entities.Main;
using Loyalty.DataAccess.Repositories.Abstractions.Main;

namespace Loyalty.Business.Services.Implementations.Main
{
    public class StatisticsService : IStatisticsService
    {
        private readonly ITransactionRepository _transactionRepository;

        public StatisticsService(ITransactionRepository transactionRepository)
        {
            _transactionRepository = transactionRepository;
        }

        public async Task<ServiceResult<DashboardStatisticsDto>> GetDashboardAsync(
            int tenantId,
            DateOnly startDate,
            DateOnly endDate)
        {
            var transactions = await _transactionRepository.GetByTenantIdDateAsync(
                tenantId, startDate, endDate);

            var transactionList = transactions.ToList();

            var totalPointsEarned = transactionList
                .Where(t => t.Type == TransactionType.Earned || t.Type == TransactionType.Bonus)
                .Sum(t => t.Points);

            var totalPointsSpent = transactionList
                .Where(t => t.Type == TransactionType.Redeemed)
                .Sum(t => t.Points);

            var uniqueCustomers = transactionList
                .Select(t => t.LoyaltyCardId)
                .Distinct()
                .Count();

            var dailyStats = transactionList
                .GroupBy(t => DateOnly.FromDateTime(t.LocalOccurredAt.DateTime))
                .Select(g => new DailyStatDto
                {
                    Date = g.Key,
                    TransactionCount = g.Count(),
                    PointsEarned = g
                        .Where(t => t.Type == TransactionType.Earned || t.Type == TransactionType.Bonus)
                        .Sum(t => t.Points),
                    PointsSpent = g
                        .Where(t => t.Type == TransactionType.Redeemed)
                        .Sum(t => t.Points),
                })
                .OrderBy(d => d.Date)
                .ToList();

            var dto = new DashboardStatisticsDto
            {
                TotalTransactions = transactionList.Count,
                TotalPointsEarned = totalPointsEarned,
                TotalPointsSpent = totalPointsSpent,
                UniqueCustomers = uniqueCustomers,
                DailyStats = dailyStats,
            };

            return ServiceResult<DashboardStatisticsDto>.Ok(dto);
        }
    }
}

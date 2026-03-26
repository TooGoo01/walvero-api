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
            DateOnly endDate,
            int? programId = null)
        {
            var transactions = await _transactionRepository.GetByTenantIdDateAsync(
                tenantId, startDate, endDate);

            var transactionList = transactions.ToList();

            // ProgramId filteri
            if (programId.HasValue && programId.Value > 0)
            {
                transactionList = transactionList
                    .Where(t => t.LoyaltyCard != null && t.LoyaltyCard.LoyaltyProgramId == programId.Value)
                    .ToList();
            }

            // Qazanma əməliyyat SAYI (delta cəmi deyil!)
            var totalPointsEarned = transactionList
                .Where(t => t.Type == TransactionType.Earned || t.Type == TransactionType.Bonus)
                .Sum(t => Math.Abs(t.Points));

            // Xərcləmə əməliyyat miqdarı
            var totalPointsSpent = transactionList
                .Where(t => t.Type == TransactionType.Redeemed)
                .Sum(t => Math.Abs(t.Points));

            // Unikal müştəri sayı - LoyaltyCard.CustomerId + fallback
            var customerIds = transactionList
                .Where(t => t.LoyaltyCard?.CustomerId > 0)
                .Select(t => t.LoyaltyCard!.CustomerId)
                .Distinct()
                .ToList();

            // Customer navigation null olarsa, LoyaltyCard.Customer-dən al
            if (customerIds.Count == 0 && transactionList.Count > 0)
            {
                customerIds = transactionList
                    .Where(t => t.LoyaltyCard?.Customer != null)
                    .Select(t => t.LoyaltyCard!.Customer!.Id)
                    .Distinct()
                    .ToList();
            }

            // Hələ də 0-dırsa, LoyaltyCardId ilə distinct (hər kart = fərqli müştəri)
            var uniqueCustomers = customerIds.Count > 0
                ? customerIds.Count
                : transactionList.Select(t => t.LoyaltyCardId).Distinct().Count();

            var dailyStats = transactionList
                .GroupBy(t => DateOnly.FromDateTime(t.LocalOccurredAt.DateTime))
                .Select(g => new DailyStatDto
                {
                    Date = g.Key,
                    TransactionCount = g.Count(),
                    PointsEarned = g
                        .Where(t => t.Type == TransactionType.Earned || t.Type == TransactionType.Bonus)
                        .Sum(t => Math.Abs(t.Points)),
                    PointsSpent = g
                        .Where(t => t.Type == TransactionType.Redeemed)
                        .Sum(t => Math.Abs(t.Points)),
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

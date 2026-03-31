using Loyalty.Business.Dtos.Get.Main;
using Loyalty.Business.Results;
using Loyalty.Business.Services.Abstractions.Main;
using Loyalty.DataAccess.Entities.Main;
using Loyalty.DataAccess.Repositories.Abstractions.Main;
using Microsoft.EntityFrameworkCore;

namespace Loyalty.Business.Services.Implementations.Main
{
    public class StatisticsService : IStatisticsService
    {
        private readonly ITransactionRepository _transactionRepository;
        private readonly ILoyaltyCardRepository _loyaltyCardRepository;

        public StatisticsService(ITransactionRepository transactionRepository, ILoyaltyCardRepository loyaltyCardRepository)
        {
            _transactionRepository = transactionRepository;
            _loyaltyCardRepository = loyaltyCardRepository;
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

            // Unikal müştəri sayı - birbaşa LoyaltyCard cədvəlindən (transaksiyası olmayan müştərilər də daxil)
            var cardQuery = _loyaltyCardRepository.GetAsQueryable()
                .Where(c => c.TenantId == tenantId && c.IsActive);

            if (programId.HasValue && programId.Value > 0)
            {
                cardQuery = cardQuery.Where(c => c.LoyaltyProgramId == programId.Value);
            }

            var uniqueCustomers = await cardQuery
                .Where(c => c.CustomerId > 0)
                .Select(c => c.CustomerId)
                .Distinct()
                .CountAsync();

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

            // Son 20 əməliyyat
            var recentTransactions = transactionList
                .OrderByDescending(t => t.CreatedAt)
                .Take(20)
                .Select(t => new RecentTransactionDto
                {
                    Id = t.Id,
                    CustomerName = t.LoyaltyCard?.Customer?.Name != null
                        ? $"{t.LoyaltyCard.Customer.Name} {t.LoyaltyCard.Customer.Surname}".Trim()
                        : t.LoyaltyCard?.BarcodeValue ?? "Unknown",
                    Type = t.Type.ToString(),
                    Points = t.Points,
                    Amount = t.Amount,
                    BalanceAfter = t.BalanceAfter,
                    CreatedAt = t.CreatedAt,
                })
                .ToList();

            var dto = new DashboardStatisticsDto
            {
                TotalTransactions = transactionList.Count,
                TotalPointsEarned = totalPointsEarned,
                TotalPointsSpent = totalPointsSpent,
                UniqueCustomers = uniqueCustomers,
                DailyStats = dailyStats,
                RecentTransactions = recentTransactions,
            };

            return ServiceResult<DashboardStatisticsDto>.Ok(dto);
        }
    }
}

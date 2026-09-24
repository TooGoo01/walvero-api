using Loyalty.Business.Dtos.Get.Main;
using Loyalty.Business.Results;
using Loyalty.Business.Services.Abstractions.Main;
using Loyalty.DataAccess.Context;
using Loyalty.DataAccess.Entities.Main;
using Loyalty.DataAccess.Enums;
using Microsoft.EntityFrameworkCore;

namespace Loyalty.Business.Services.Implementations.Main
{
    public class StatisticsService : IStatisticsService
    {
        private readonly LoyaltyWalletDbContext _dbContext;

        public StatisticsService(LoyaltyWalletDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public Task<int> GetUniqueCustomerCountAsync(int tenantId, CancellationToken ct = default)
            => _dbContext.Set<LoyaltyCard>()
                .Where(c => c.TenantId == tenantId && c.IsActive && c.CustomerId > 0)
                .Select(c => c.CustomerId)
                .Distinct()
                .CountAsync(ct);

        /// <summary>
        /// Dashboard v2 — bütün aggregatlar SQL-side (xam tranzaksiyalar yüklənmir;
        /// illik aralıq = maks ~366 aggregate sətri). Yalnız Applied tx-lər sayılır.
        /// groupBy: "day" | "week" (ISO Bazar ertəsi) | "month".
        /// </summary>
        public async Task<ServiceResult<DashboardStatisticsDto>> GetDashboardAsync(
            int tenantId,
            DateOnly startDate,
            DateOnly endDate,
            int? programId = null,
            string groupBy = "day")
        {
            var startDt = startDate.ToDateTime(TimeOnly.MinValue);
            var endDt = endDate.ToDateTime(TimeOnly.MaxValue);

            IQueryable<Transaction> TxQuery(DateTime s, DateTime e)
            {
                var q = _dbContext.Set<Transaction>()
                    .Where(t => t.IsActive
                             && t.Status == RedeemStatus.Applied
                             && t.LoyaltyCard.TenantId == tenantId
                             && t.LocalOccurredAt >= s
                             && t.LocalOccurredAt <= e);
                if (programId.HasValue && programId.Value > 0)
                    q = q.Where(t => t.LoyaltyCard.LoyaltyProgramId == programId.Value);
                return q;
            }

            // 1) Günlük aggregate (SQL GROUP BY) — seriyaların əsası
            var dailyRaw = await TxQuery(startDt, endDt)
                .GroupBy(t => t.LocalOccurredAt.Date)
                .Select(g => new
                {
                    Date = g.Key,
                    Count = g.Count(),
                    Earned = g.Sum(t =>
                        t.Type == TransactionType.Earned || t.Type == TransactionType.Bonus
                            ? Math.Abs(t.Points) : 0),
                    Spent = g.Sum(t =>
                        t.Type == TransactionType.Redeemed ? Math.Abs(t.Points) : 0),
                })
                .OrderBy(x => x.Date)
                .ToListAsync();

            // 2) Yeni müştərilər (günlük)
            var newCustomersRaw = await _dbContext.Set<Customer>()
                .Where(c => c.TenantId == tenantId && c.RegDate >= startDt && c.RegDate <= endDt)
                .GroupBy(c => c.RegDate.Date)
                .Select(g => new { Date = g.Key, Count = g.Count() })
                .ToListAsync();
            var newCustomersMap = newCustomersRaw.ToDictionary(x => DateOnly.FromDateTime(x.Date), x => x.Count);

            // 3) Bucket roll-up (day → week/month)
            DateOnly BucketOf(DateOnly d) => groupBy switch
            {
                "week" => d.AddDays(-((((int)d.DayOfWeek) + 6) % 7)), // ISO: Bazar ertəsi
                "month" => new DateOnly(d.Year, d.Month, 1),
                _ => d,
            };

            var buckets = new SortedDictionary<DateOnly, PeriodStatDto>();
            foreach (var row in dailyRaw)
            {
                var d = DateOnly.FromDateTime(row.Date);
                var key = BucketOf(d);
                if (!buckets.TryGetValue(key, out var b))
                    buckets[key] = b = new PeriodStatDto { PeriodStart = key };
                b.TransactionCount += row.Count;
                b.PointsEarned += row.Earned;
                b.PointsSpent += row.Spent;
            }
            foreach (var (d, cnt) in newCustomersMap)
            {
                var key = BucketOf(d);
                if (!buckets.TryGetValue(key, out var b))
                    buckets[key] = b = new PeriodStatDto { PeriodStart = key };
                b.NewCustomers += cnt;
            }

            // 4) Əvvəlki bərabər dövr (delta % üçün) — yalnız totallar
            var spanDays = endDate.DayNumber - startDate.DayNumber + 1;
            var prevStart = startDate.AddDays(-spanDays).ToDateTime(TimeOnly.MinValue);
            var prevEnd = startDate.AddDays(-1).ToDateTime(TimeOnly.MaxValue);

            var prevAgg = await TxQuery(prevStart, prevEnd)
                .GroupBy(_ => 1)
                .Select(g => new
                {
                    Count = g.Count(),
                    Earned = g.Sum(t =>
                        t.Type == TransactionType.Earned || t.Type == TransactionType.Bonus
                            ? Math.Abs(t.Points) : 0),
                    Spent = g.Sum(t =>
                        t.Type == TransactionType.Redeemed ? Math.Abs(t.Points) : 0),
                })
                .FirstOrDefaultAsync();

            var prevNewCustomers = await _dbContext.Set<Customer>()
                .CountAsync(c => c.TenantId == tenantId && c.RegDate >= prevStart && c.RegDate <= prevEnd);

            // 5) Kart statistikası (dövrdən asılı deyil — cari vəziyyət)
            var cardQuery = _dbContext.Set<LoyaltyCard>()
                .Where(c => c.TenantId == tenantId && c.IsActive && c.Status == CardStatus.Active);
            if (programId.HasValue && programId.Value > 0)
                cardQuery = cardQuery.Where(c => c.LoyaltyProgramId == programId.Value);

            var activeCards = await cardQuery.CountAsync();
            var iosCards = await cardQuery.CountAsync(c => c.IsIOS);
            var androidCards = await cardQuery.CountAsync(c => c.IsAndroid);

            var uniqueCustomers = await cardQuery
                .Where(c => c.CustomerId > 0)
                .Select(c => c.CustomerId)
                .Distinct()
                .CountAsync();

            // 6) Filial breakdown (SQL GROUP BY BranchId → ad map)
            var branchAgg = await TxQuery(startDt, endDt)
                .Where(t => t.BranchId != null)
                .GroupBy(t => t.BranchId!.Value)
                .Select(g => new
                {
                    BranchId = g.Key,
                    Count = g.Count(),
                    Earned = g.Sum(t =>
                        t.Type == TransactionType.Earned || t.Type == TransactionType.Bonus
                            ? Math.Abs(t.Points) : 0),
                })
                .OrderByDescending(x => x.Earned)
                .Take(8)
                .ToListAsync();

            var branchNames = await _dbContext.Set<Branch>()
                .Where(b => b.TenantId == tenantId)
                .Select(b => new { b.Id, b.Name })
                .ToDictionaryAsync(b => b.Id, b => b.Name);

            // 7) Son 20 tranzaksiya (yalnız 20 sətir yüklənir)
            var recent = await TxQuery(startDt, endDt)
                .OrderByDescending(t => t.LocalOccurredAt)
                .Take(20)
                .Select(t => new
                {
                    t.Id,
                    FirstName = t.LoyaltyCard.Customer != null ? t.LoyaltyCard.Customer.FirstName : null,
                    LastName = t.LoyaltyCard.Customer != null ? t.LoyaltyCard.Customer.LastName : null,
                    t.LoyaltyCard.BarcodeValue,
                    t.Type,
                    t.Points,
                    t.Amount,
                    t.BalanceAfter,
                    t.LocalOccurredAt,
                })
                .ToListAsync();

            var series = buckets.Values.ToList();

            var dto = new DashboardStatisticsDto
            {
                TotalTransactions = dailyRaw.Sum(x => x.Count),
                TotalPointsEarned = dailyRaw.Sum(x => x.Earned),
                TotalPointsSpent = dailyRaw.Sum(x => x.Spent),
                UniqueCustomers = uniqueCustomers,
                NewCustomers = newCustomersRaw.Sum(x => x.Count),
                ActiveCards = activeCards,
                IosCards = iosCards,
                AndroidCards = androidCards,
                Series = series,
                // Geri uyğunluq: günlük seriya köhnə sahədə də qalır
                DailyStats = dailyRaw.Select(x => new DailyStatDto
                {
                    Date = DateOnly.FromDateTime(x.Date),
                    TransactionCount = x.Count,
                    PointsEarned = x.Earned,
                    PointsSpent = x.Spent,
                }).ToList(),
                Previous = new PeriodTotalsDto
                {
                    TotalTransactions = prevAgg?.Count ?? 0,
                    TotalPointsEarned = prevAgg?.Earned ?? 0,
                    TotalPointsSpent = prevAgg?.Spent ?? 0,
                    NewCustomers = prevNewCustomers,
                },
                BranchBreakdown = branchAgg.Select(x => new BranchStatDto
                {
                    BranchId = x.BranchId,
                    BranchName = branchNames.TryGetValue(x.BranchId, out var n) ? n : $"#{x.BranchId}",
                    TransactionCount = x.Count,
                    PointsEarned = x.Earned,
                }).ToList(),
                RecentTransactions = recent.Select(t => new RecentTransactionDto
                {
                    Id = t.Id,
                    CustomerName = !string.IsNullOrWhiteSpace(t.FirstName)
                        ? $"{t.FirstName} {t.LastName}".Trim()
                        : t.BarcodeValue ?? "Unknown",
                    Type = t.Type.ToString(),
                    Points = t.Points,
                    Amount = t.Amount,
                    BalanceAfter = t.BalanceAfter,
                    CreatedAt = t.LocalOccurredAt,
                }).ToList(),
            };

            return ServiceResult<DashboardStatisticsDto>.Ok(dto);
        }
    }
}

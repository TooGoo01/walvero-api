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

            // Günlük tx aggregate (SQL GROUP BY) — cari və əvvəlki pəncərə üçün ortaq
            async Task<List<(DateOnly Date, int Count, decimal Earned, decimal Spent)>> TxDailyAsync(DateTime s, DateTime e)
            {
                var raw = await TxQuery(s, e)
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
                return raw.Select(x => (DateOnly.FromDateTime(x.Date), x.Count, x.Earned, x.Spent)).ToList();
            }

            async Task<Dictionary<DateOnly, int>> NewCustomersDailyAsync(DateTime s, DateTime e)
            {
                var raw = await _dbContext.Set<Customer>()
                    .Where(c => c.TenantId == tenantId && c.RegDate >= s && c.RegDate <= e)
                    .GroupBy(c => c.RegDate.Date)
                    .Select(g => new { Date = g.Key, Count = g.Count() })
                    .ToListAsync();
                return raw.ToDictionary(x => DateOnly.FromDateTime(x.Date), x => x.Count);
            }

            // İstifadə göstəriciləri: ziyarət = kart × gün (SQL GROUP BY, yalnız kart id-ləri yüklənir)
            async Task<(int Engaged, int Visits, int Repeat)> VisitStatsAsync(DateTime s, DateTime e)
            {
                var visitCardIds = await TxQuery(s, e)
                    .GroupBy(t => new { t.LoyaltyCardId, D = t.LocalOccurredAt.Date })
                    .Select(g => g.Key.LoyaltyCardId)
                    .ToListAsync();
                var perCard = visitCardIds.GroupBy(id => id).Select(g => g.Count()).ToList();
                return (perCard.Count, visitCardIds.Count, perCard.Count(v => v >= 2));
            }

            Task<int> ActiveCardsInWindowAsync(DateTime s, DateTime e)
                => TxQuery(s, e).Select(t => t.LoyaltyCardId).Distinct().CountAsync();

            // Bucket roll-up (day → week/month)
            DateOnly BucketOf(DateOnly d) => groupBy switch
            {
                "week" => d.AddDays(-((((int)d.DayOfWeek) + 6) % 7)), // ISO: Bazar ertəsi
                "month" => new DateOnly(d.Year, d.Month, 1),
                _ => d,
            };

            List<PeriodStatDto> BuildSeries(
                List<(DateOnly Date, int Count, decimal Earned, decimal Spent)> daily,
                Dictionary<DateOnly, int> newCust)
            {
                var buckets = new SortedDictionary<DateOnly, PeriodStatDto>();
                foreach (var row in daily)
                {
                    var key = BucketOf(row.Date);
                    if (!buckets.TryGetValue(key, out var b))
                        buckets[key] = b = new PeriodStatDto { PeriodStart = key };
                    b.TransactionCount += row.Count;
                    b.PointsEarned += row.Earned;
                    b.PointsSpent += row.Spent;
                }
                foreach (var (d, cnt) in newCust)
                {
                    var key = BucketOf(d);
                    if (!buckets.TryGetValue(key, out var b))
                        buckets[key] = b = new PeriodStatDto { PeriodStart = key };
                    b.NewCustomers += cnt;
                }
                return buckets.Values.ToList();
            }

            // 1) Cari pəncərə
            var dailyRaw = await TxDailyAsync(startDt, endDt);
            var newCustomersMap = await NewCustomersDailyAsync(startDt, endDt);
            var series = BuildSeries(dailyRaw, newCustomersMap);

            // 2) Əvvəlki bərabər dövr — həm seriya (overlay müqayisə), həm totallar (delta %)
            var spanDays = endDate.DayNumber - startDate.DayNumber + 1;
            var prevStart = startDate.AddDays(-spanDays).ToDateTime(TimeOnly.MinValue);
            var prevEnd = startDate.AddDays(-1).ToDateTime(TimeOnly.MaxValue);

            var prevDaily = await TxDailyAsync(prevStart, prevEnd);
            var prevNewCustMap = await NewCustomersDailyAsync(prevStart, prevEnd);
            var previousSeries = BuildSeries(prevDaily, prevNewCustMap);
            var prevNewCustomers = prevNewCustMap.Values.Sum();

            // 3) İstifadə göstəriciləri — seçilən dövr + əvvəlki dövr
            var visits = await VisitStatsAsync(startDt, endDt);
            var prevVisits = await VisitStatsAsync(prevStart, prevEnd);

            // 4) Son 30 gündə aktiv kart (dövr tab-ından asılı deyil; endDate-ə qədər)
            var a30Start = endDate.AddDays(-29).ToDateTime(TimeOnly.MinValue);
            var a30PrevStart = endDate.AddDays(-59).ToDateTime(TimeOnly.MinValue);
            var a30PrevEnd = endDate.AddDays(-30).ToDateTime(TimeOnly.MaxValue);
            var activeCards30d = await ActiveCardsInWindowAsync(a30Start, endDt);
            var activeCards30dPrev = await ActiveCardsInWindowAsync(a30PrevStart, a30PrevEnd);

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

            // 5b) Müştəri bazası (dövrdən asılı deyil)
            var totalCustomers = await _dbContext.Set<Customer>()
                .CountAsync(c => c.TenantId == tenantId && c.IsActive);
            var appCustomers = await _dbContext.Set<Customer>()
                .CountAsync(c => c.TenantId == tenantId && c.IsActive && c.applicationUserId != null);
            var walletCustomers = await cardQuery
                .Where(c => (c.IsIOS || c.IsAndroid) && c.CustomerId > 0)
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

            // 8) Bildirişlər — dövrdəki göndərişlər (RegDate UTC) + alıcıların 7 günlük geri dönüşü
            const int notifWindow = 7;
            var notifLogs = await _dbContext.Set<TenantNotificationLog>().AsNoTracking()
                .Where(l => l.TenantId == tenantId && l.IsActive && l.RegDate >= startDt && l.RegDate <= endDt)
                .OrderByDescending(l => l.RegDate)
                .Take(500)
                .Select(l => new { l.Id, l.Source, l.RecipientCount, l.AppSent, l.WalletSent })
                .ToListAsync();
            var notifIds = notifLogs.Select(l => l.Id).ToList();
            var notifWithRows = notifIds.Count == 0
                ? new HashSet<int>()
                : (await _dbContext.Set<TenantNotificationRecipient>().AsNoTracking()
                    .Where(r => notifIds.Contains(r.TenantNotificationLogId))
                    .Select(r => r.TenantNotificationLogId).Distinct().ToListAsync()).ToHashSet();
            var notifAttr = await NotificationAttribution.ByLogAsync(_dbContext, notifIds, notifWindow, default);
            var attributedRecipients = notifLogs.Where(l => notifWithRows.Contains(l.Id)).Sum(l => l.RecipientCount);
            var notifVisits = notifAttr.Values.Sum(a => a.Visits);
            var notifications = new NotificationStatsDto
            {
                Sends = notifLogs.Count,
                BirthdaySends = notifLogs.Count(l => l.Source == TenantNotificationLog.SourceBirthday),
                Recipients = notifLogs.Sum(l => l.RecipientCount),
                AppSent = notifLogs.Sum(l => l.AppSent ?? 0),
                WalletSent = notifLogs.Sum(l => l.WalletSent ?? 0),
                AttributedRecipients = attributedRecipients,
                Visits = notifVisits,
                VisitRate = attributedRecipients == 0 ? 0 : Math.Round(notifVisits * 100.0 / attributedRecipients, 1),
                Revenue = notifAttr.Values.Sum(a => a.Revenue),
                AttributionDays = notifWindow,
            };

            var dto = new DashboardStatisticsDto
            {
                Notifications = notifications,
                TotalTransactions = dailyRaw.Sum(x => x.Count),
                TotalPointsEarned = dailyRaw.Sum(x => x.Earned),
                TotalPointsSpent = dailyRaw.Sum(x => x.Spent),
                UniqueCustomers = uniqueCustomers,
                NewCustomers = newCustomersMap.Values.Sum(),
                ActiveCards = activeCards,
                IosCards = iosCards,
                AndroidCards = androidCards,
                TotalCustomers = totalCustomers,
                AppCustomers = appCustomers,
                WalletCustomers = walletCustomers,
                ActiveCards30d = activeCards30d,
                ActiveCards30dPrev = activeCards30dPrev,
                EngagedCards = visits.Engaged,
                TotalVisits = visits.Visits,
                RepeatCards = visits.Repeat,
                Series = series,
                PreviousSeries = previousSeries,
                // Geri uyğunluq: günlük seriya köhnə sahədə də qalır
                DailyStats = dailyRaw.Select(x => new DailyStatDto
                {
                    Date = x.Date,
                    TransactionCount = x.Count,
                    PointsEarned = x.Earned,
                    PointsSpent = x.Spent,
                }).ToList(),
                Previous = new PeriodTotalsDto
                {
                    TotalTransactions = prevDaily.Sum(x => x.Count),
                    TotalPointsEarned = prevDaily.Sum(x => x.Earned),
                    TotalPointsSpent = prevDaily.Sum(x => x.Spent),
                    NewCustomers = prevNewCustomers,
                    EngagedCards = prevVisits.Engaged,
                    TotalVisits = prevVisits.Visits,
                    RepeatCards = prevVisits.Repeat,
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

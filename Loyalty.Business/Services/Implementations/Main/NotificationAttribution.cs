using Loyalty.DataAccess.Context;
using Loyalty.DataAccess.Entities.Main;
using Loyalty.DataAccess.Enums;
using Microsoft.EntityFrameworkCore;

namespace Loyalty.Business.Services.Implementations.Main
{
    /// <summary>
    /// Bildiriş geri dönüşü (attribution): alıcının (TenantNotificationRecipient) göndərişdən sonrakı N gündə Applied Earned/Redeemed
    /// tranzaksiyası = "gəldi"; dövriyyə = Earned tx Amount cəmi. Müştərinin tenant-dakı bütün kartları üzrə. Vaxt: Transaction.RegDate (UTC).
    /// Tarixçə (bir səhifə) və dashboard ortaq istifadə edir.
    /// </summary>
    internal static class NotificationAttribution
    {
        public readonly record struct Stat(int Visits, decimal Revenue);

        public static async Task<Dictionary<int, Stat>> ByLogAsync(LoyaltyWalletDbContext db, IReadOnlyCollection<int> logIds, int windowDays, CancellationToken ct)
        {
            if (logIds.Count == 0) return new Dictionary<int, Stat>();
            var ids = logIds.ToList();
            double window = windowDays;
            // SQL: (log, müştəri) səviyyəsində qruplaşdırma — COUNT(DISTINCT) translyasiyasından asılı olmamaq üçün yekun yaddaşda
            var rows = await (
                from r in db.TenantNotificationRecipients.AsNoTracking()
                join l in db.TenantNotificationLogs.AsNoTracking() on r.TenantNotificationLogId equals l.Id
                join c in db.LoyaltyCards.AsNoTracking() on new { r.CustomerId, r.TenantId } equals new { c.CustomerId, c.TenantId }
                join t in db.Transactions.AsNoTracking() on c.Id equals t.LoyaltyCardId
                where ids.Contains(l.Id)
                      && t.IsActive && t.Status == RedeemStatus.Applied
                      && (t.Type == TransactionType.Earned || t.Type == TransactionType.Redeemed)
                      && t.RegDate > l.RegDate && t.RegDate <= l.RegDate.AddDays(window)
                group t by new { LogId = l.Id, r.CustomerId } into g
                select new
                {
                    g.Key.LogId,
                    g.Key.CustomerId,
                    Revenue = g.Sum(x => x.Type == TransactionType.Earned ? (x.Amount ?? 0m) : 0m)
                }).ToListAsync(ct);
            return rows.GroupBy(x => x.LogId).ToDictionary(g => g.Key, g => new Stat(g.Count(), g.Sum(x => x.Revenue)));
        }
    }
}

using Loyalty.DataAccess.Entities.Main;

namespace Loyalty.Business.Helpers
{
    /// <summary>
    /// Kart balansının son istifadə tarixi (ambassador xalı). <see cref="LoyaltyCard.ExpiresAt"/> dolu və keçmişdirsə
    /// balans bitmiş sayılır — job sıfırlamamış olsa belə istifadə bloklanır. null = müddətsiz.
    /// </summary>
    public static class PointsExpiry
    {
        public static bool IsExpired(DateTime? expiresAt, DateTime? now = null)
            => expiresAt.HasValue && expiresAt.Value <= (now ?? DateTime.UtcNow);

        /// <summary>
        /// DB-dən oxunan dəyər Kind=Unspecified gəlir → JSON-da "Z" olmur → browser yerli vaxt kimi oxuyar.
        /// DTO-ya verməzdən əvvəl UTC kimi işarələ ki, serializasiya "…Z" ilə getsin.
        /// </summary>
        public static DateTime? AsUtc(DateTime? value)
            => value.HasValue ? DateTime.SpecifyKind(value.Value, DateTimeKind.Utc) : null;

        public static bool IsPointsExpired(this LoyaltyCard card, DateTime? now = null)
            => IsExpired(card.ExpiresAt, now);

        /// <summary>
        /// Xal verilərkən kartın bitmə tarixinin yeni dəyəri:
        /// verilibsə → o (UTC); verilməyibsə və mövcud tarix keçmişdirsə → null (yeni xal dərhal sıfırlanmasın); əks halda → mövcud.
        /// </summary>
        public static DateTime? ResolveOnCredit(DateTime? current, DateTime? requested, DateTime now)
        {
            if (requested.HasValue) return DateTime.SpecifyKind(requested.Value.ToUniversalTime(), DateTimeKind.Utc);
            if (IsExpired(current, now)) return null;
            return current;
        }
    }
}

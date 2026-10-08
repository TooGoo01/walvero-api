using Loyalty.DataAccess.Abstractions;
using Loyalty.DataAccess.Entities.Base;

namespace Loyalty.DataAccess.Entities.Main
{
    /// <summary>
    /// Müştəriyə bu ad günü üçün bildiriş/hədiyyə göndərilib — unikal (TenantId, CustomerId, Year) idempotentlik verir.
    /// Year = ad gününün ili (N gün əvvəl göndərişdə dekabr→yanvar keçidi üçün gələn il).
    /// </summary>
    public class BirthdayNotificationLog : EntityBase, IEntity
    {
        public int TenantId { get; set; }
        public int CustomerId { get; set; }
        public int Year { get; set; }
        public DateTime SentAtUtc { get; set; }
        /// <summary>Göstərmə vahidində: ₼ / möhür / məhsul sayı. null = hədiyyə verilməyib (uyğun kart yox və ya söndürülü).</summary>
        public decimal? GiftAmount { get; set; }
        public int? LoyaltyCardId { get; set; }
        /// <summary>Ayarda seçilmiş növ (BirthdayGiftType). Dolu + GiftAmount null = hədiyyə aktiv idi, uyğun kart tapılmadı.</summary>
        public int? GiftType { get; set; }
    }
}

using Loyalty.DataAccess.Abstractions;
using Loyalty.DataAccess.Entities.Base;

namespace Loyalty.DataAccess.Entities.Main
{
    /// <summary>Müştəriyə bu il ad günü bildirişi/hədiyyəsi göndərilib — unikal (TenantId, CustomerId, Year) idempotentlik verir.</summary>
    public class BirthdayNotificationLog : EntityBase, IEntity
    {
        public int TenantId { get; set; }
        public int CustomerId { get; set; }
        public int Year { get; set; }
        public DateTime SentAtUtc { get; set; }
        public decimal? GiftAmount { get; set; }
        public int? LoyaltyCardId { get; set; }
    }
}

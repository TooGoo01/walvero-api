using Loyalty.DataAccess.Abstractions;
using Loyalty.DataAccess.Entities.Base;

namespace Loyalty.DataAccess.Entities.Main
{
    public enum BonusGrantStatus
    {
        Active = 0,
        Expired = 1,
        Closed = 2
    }

    /// <summary>
    /// Müddətli bonus (məs. ad günü hədiyyəsi). LoyaltyCard.ExpiresAt bütün balansı sıfırlayır (ambassador) — bu isə
    /// yalnız bonusun istifadə olunmamış hissəsini silir ("bonus əvvəl xərclənir" qaydası, PointsExpiryService).
    /// </summary>
    public class BonusGrant : EntityBase, IEntity
    {
        public int TenantId { get; set; }
        public int LoyaltyCardId { get; set; }
        /// <summary>"birthday"</summary>
        public string Source { get; set; } = "birthday";
        public decimal Amount { get; set; }
        public DateTime GrantedAtUtc { get; set; }
        public DateTime? ExpiresAtUtc { get; set; }
        public BonusGrantStatus Status { get; set; } = BonusGrantStatus.Active;
        public decimal? ExpiredAmount { get; set; }
    }
}

using Loyalty.DataAccess.Abstractions;
using Loyalty.DataAccess.Entities.Base;

namespace Loyalty.DataAccess.Entities.Main
{
    /// <summary>
    /// POS (iiko) sifariş hadisəsinin emal markeri — idempotency açarı (<see cref="OrderKey"/> unikal).
    /// Əvvəl bu məqsədlə Transactions-a "OrderClosed marker" (Adjusted) sətri yazılırdı; o sətir
    /// tarixçədə dublikat "ödəniş" kimi görünür və statistikada sayılırdı. İndi balansı dəyişməyən
    /// audit məlumatı burada saxlanır, Transactions-da yalnız real earn/redeem sətirləri qalır.
    /// </summary>
    public class PosOrderMarker : EntityBase, IEntity
    {
        public int LoyaltyCardId { get; set; }
        /// <summary>"{src}:{orderId}:{OrderClosed|OrderReturned}" — köhnə Transaction.OrderId açarı ilə eyni format.</summary>
        public string OrderKey { get; set; } = string.Empty;
        public string? Source { get; set; }          // iiko_pos / rkeeper ...
        public string? ExternalOrderId { get; set; }
        public string? EventType { get; set; }       // OrderClosed / OrderReturned
        public string? OrderNumber { get; set; }
        public decimal TotalSum { get; set; }
        public decimal EarnedValue { get; set; }
        public decimal WalletRedeem { get; set; }
        public int DiscountRedeemUnits { get; set; }
        public decimal WelcomeBonus { get; set; }
        public decimal BalanceAfter { get; set; }
        public int? BranchId { get; set; }
        public string? Note { get; set; }            // "no-op" / köhnə marker description (backfill)
    }
}

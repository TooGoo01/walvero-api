using Loyalty.DataAccess.Abstractions;
using Loyalty.DataAccess.Entities.Base;

namespace Loyalty.DataAccess.Entities.Main
{
    /// <summary>
    /// Hədiyyə ola bilən kassa məhsulu (iiko ProductNumber). Bir hədiyyəyə çox məhsul — məs. Cupcup "Pulsuz 1 dessert" = 76 desert.
    /// Tək məhsullu köhnə yol Reward.ExternalProductId ilə qalır.
    /// </summary>
    public class RewardPosProduct : EntityBase, IEntity
    {
        public int RewardId { get; set; }
        public string Code { get; set; } = string.Empty;
        public string? ProductName { get; set; }

        public Reward Reward { get; set; } = null!;
    }
}

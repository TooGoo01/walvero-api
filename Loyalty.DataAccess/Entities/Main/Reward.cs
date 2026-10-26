using Loyalty.DataAccess.Abstractions;
using Loyalty.DataAccess.Entities.Base;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Loyalty.DataAccess.Entities.Main
{
    public class Reward : EntityBase, IEntity
    {
        public int LoyaltyProgramId { get; set; }

        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public int PointsCost { get; set; }

        public string? ImageUrl { get; set; }
        /// <summary>External product ID (e.g. Monyo POS product ID) for reward redemption.</summary>
        public string? ExternalProductId { get; set; }

        public bool IsActive { get; set; } = true;
        public int? StockQuantity { get; set; } // null = unlimited

        /// <summary>Kassada bu hədiyyəni verən endirim növü (iiko DiscountTypeId) — sifarişdə həmin endirim = bu hədiyyə.</summary>
        public string? PosDiscountTypeId { get; set; }
        /// <summary>Kassa idxalının mənbə açarı ("{iiko proqram id}:{qiymət}") — təkrar idxal eyni hədiyyəni yaratmasın.</summary>
        public string? PosSourceKey { get; set; }

        // Navigation Property
        public LoyaltyProgram LoyaltyProgram { get; set; } = null!;
        public ICollection<RewardPosProduct> PosProducts { get; set; } = new List<RewardPosProduct>();
    }
}

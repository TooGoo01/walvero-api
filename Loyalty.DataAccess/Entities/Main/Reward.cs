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

        public bool IsActive { get; set; } = true;
        public int? StockQuantity { get; set; } // null = unlimited

        // Navigation Property
        public LoyaltyProgram LoyaltyProgram { get; set; } = null!;
    }
}

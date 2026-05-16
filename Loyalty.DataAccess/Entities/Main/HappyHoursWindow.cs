using Loyalty.DataAccess.Abstractions;
using Loyalty.DataAccess.Entities.Base;
using System;

namespace Loyalty.DataAccess.Entities.Main
{
    public class HappyHoursWindow : EntityBase, IEntity
    {
        public int LoyaltyProgramId { get; set; }
        public LoyaltyProgram LoyaltyProgram { get; set; } = null!;

        public byte? DayOfWeek { get; set; }
        public DateOnly? SpecificDate { get; set; }
        public TimeOnly StartTime { get; set; }
        public TimeOnly EndTime { get; set; }

        public int PercentCash { get; set; }
        public int PercentCard { get; set; }

        public string? Label { get; set; }
    }
}

using Loyalty.DataAccess.Entities.Main;
using System;
using System.Linq;

namespace Loyalty.Business.Helpers
{
    public static class HappyHoursRateResolver
    {
        public static (int percentCash, int percentCard) Resolve(LoyaltyProgram program, DateTime nowLocal)
        {
            if (program is null) return (0, 0);

            var date = DateOnly.FromDateTime(nowLocal);
            var time = TimeOnly.FromDateTime(nowLocal);
            var dow = (byte)nowLocal.DayOfWeek;

            var window = program.HappyHoursWindows?
                .Where(w => w.SpecificDate == null || w.SpecificDate == date)
                .Where(w => w.DayOfWeek == null || w.DayOfWeek == dow)
                .Where(w => time >= w.StartTime && time < w.EndTime)
                .OrderByDescending(w => w.SpecificDate.HasValue)
                .ThenByDescending(w => w.DayOfWeek.HasValue)
                .FirstOrDefault();

            if (window != null)
                return (window.PercentCash, window.PercentCard);

            return (program.BasePercentCash ?? 0, program.BasePercentCard ?? 0);
        }
    }
}

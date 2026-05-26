using Loyalty.Business.Dtos.Post.Main;
using Loyalty.DataAccess.Entities.Main;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Loyalty.Business.Helpers
{
    public static class HappyHoursRateResolver
    {
        // Returns per-side nullable percents. null on a side = "no override; caller should fall back to tier %".
        // Non-null (incl. 0) = explicit admin override (window match always overrides; outside windows only sides with BasePercent set override).
        public static (int? percentCash, int? percentCard) Resolve(LoyaltyProgram program, DateTime nowLocal)
        {
            if (program is null) return (null, null);
            return ResolveCore(
                program.HappyHoursWindows?.Select(w => (w.DayOfWeek, w.SpecificDate, w.StartTime, w.EndTime, w.PercentCash, w.PercentCard)),
                program.BasePercentCash,
                program.BasePercentCard,
                nowLocal);
        }

        public static (int? percentCash, int? percentCard) Resolve(
            IEnumerable<HappyHoursWindowDto>? windows,
            int? basePercentCash,
            int? basePercentCard,
            DateTime nowLocal)
        {
            return ResolveCore(
                windows?.Select(w => (w.DayOfWeek, w.SpecificDate, w.StartTime, w.EndTime, w.PercentCash, w.PercentCard)),
                basePercentCash,
                basePercentCard,
                nowLocal);
        }

        private static (int? percentCash, int? percentCard) ResolveCore(
            IEnumerable<(byte? DayOfWeek, DateOnly? SpecificDate, TimeOnly StartTime, TimeOnly EndTime, int PercentCash, int PercentCard)>? windows,
            int? basePercentCash,
            int? basePercentCard,
            DateTime nowLocal)
        {
            var date = DateOnly.FromDateTime(nowLocal);
            var time = TimeOnly.FromDateTime(nowLocal);
            var dow = (byte)nowLocal.DayOfWeek;

            var window = windows?
                .Where(w => w.SpecificDate == null || w.SpecificDate == date)
                .Where(w => w.DayOfWeek == null || w.DayOfWeek == dow)
                .Where(w => time >= w.StartTime && time < w.EndTime)
                .OrderByDescending(w => w.SpecificDate.HasValue)
                .ThenByDescending(w => w.DayOfWeek.HasValue)
                .Cast<(byte? DayOfWeek, DateOnly? SpecificDate, TimeOnly StartTime, TimeOnly EndTime, int PercentCash, int PercentCard)?>()
                .FirstOrDefault();

            if (window.HasValue)
                return (window.Value.PercentCash, window.Value.PercentCard);

            return (basePercentCash, basePercentCard);
        }
    }
}

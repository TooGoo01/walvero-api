using System;

namespace Loyalty.Business.Dtos.Post.Main
{
    public class HappyHoursWindowDto
    {
        public int? Id { get; set; }
        public byte? DayOfWeek { get; set; }
        public DateOnly? SpecificDate { get; set; }
        public TimeOnly StartTime { get; set; }
        public TimeOnly EndTime { get; set; }
        public int PercentCash { get; set; }
        public int PercentCard { get; set; }
        public string? Label { get; set; }
    }
}

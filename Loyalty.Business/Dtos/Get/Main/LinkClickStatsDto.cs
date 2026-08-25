namespace Loyalty.Business.Dtos.Get.Main
{
    public class LinkClickStatsDto
    {
        public int Total { get; set; }
        public List<LinkTypeCountDto> ByLinkType { get; set; } = new();
        public List<DailyClickCountDto> Series { get; set; } = new();
    }

    public class LinkTypeCountDto
    {
        public string LinkType { get; set; } = string.Empty;
        public int Count { get; set; }
    }

    public class DailyClickCountDto
    {
        public DateOnly Date { get; set; }
        public int Count { get; set; }
    }
}

namespace Loyalty.Business.Dtos.Get.Main
{
    public class DashboardStatisticsDto
    {
        public int TotalTransactions { get; set; }
        public int TotalPointsEarned { get; set; }
        public int TotalPointsSpent { get; set; }
        public int UniqueCustomers { get; set; }
        public List<DailyStatDto> DailyStats { get; set; } = new();
    }

    public class DailyStatDto
    {
        public DateOnly Date { get; set; }
        public int TransactionCount { get; set; }
        public int PointsEarned { get; set; }
        public int PointsSpent { get; set; }
    }
}

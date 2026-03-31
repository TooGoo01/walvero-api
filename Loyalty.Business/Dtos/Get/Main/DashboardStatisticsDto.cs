namespace Loyalty.Business.Dtos.Get.Main
{
    public class DashboardStatisticsDto
    {
        public int TotalTransactions { get; set; }
        public int TotalPointsEarned { get; set; }
        public int TotalPointsSpent { get; set; }
        public int UniqueCustomers { get; set; }
        public List<DailyStatDto> DailyStats { get; set; } = new();
        public List<RecentTransactionDto> RecentTransactions { get; set; } = new();
    }

    public class DailyStatDto
    {
        public DateOnly Date { get; set; }
        public int TransactionCount { get; set; }
        public int PointsEarned { get; set; }
        public int PointsSpent { get; set; }
    }

    public class RecentTransactionDto
    {
        public int Id { get; set; }
        public string? CustomerName { get; set; }
        public string Type { get; set; } = "";
        public int Points { get; set; }
        public decimal? Amount { get; set; }
        public int BalanceAfter { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
    }
}

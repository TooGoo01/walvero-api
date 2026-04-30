namespace Loyalty.Business.Dtos.Get.Main
{
    public class DashboardStatisticsDto
    {
        public int TotalTransactions { get; set; }
        // Points decimal(18,2) — fractional dəyərlər mümkündür, ona görə DTO-da da decimal.
        public decimal TotalPointsEarned { get; set; }
        public decimal TotalPointsSpent { get; set; }
        public int UniqueCustomers { get; set; }
        public List<DailyStatDto> DailyStats { get; set; } = new();
        public List<RecentTransactionDto> RecentTransactions { get; set; } = new();
    }

    public class DailyStatDto
    {
        public DateOnly Date { get; set; }
        public int TransactionCount { get; set; }
        public decimal PointsEarned { get; set; }
        public decimal PointsSpent { get; set; }
    }

    public class RecentTransactionDto
    {
        public int Id { get; set; }
        public string? CustomerName { get; set; }
        public string Type { get; set; } = "";
        public decimal Points { get; set; }
        public decimal? Amount { get; set; }
        public decimal BalanceAfter { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
    }
}

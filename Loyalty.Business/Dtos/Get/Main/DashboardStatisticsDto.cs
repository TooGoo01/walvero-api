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

        // ── Dashboard v2 (additive) ──
        /// <summary>groupBy-a görə bucket-lənmiş seriya (day/week/month).</summary>
        public List<PeriodStatDto> Series { get; set; } = new();
        /// <summary>Əvvəlki bərabər dövrün totalları — delta % üçün.</summary>
        public PeriodTotalsDto? Previous { get; set; }
        /// <summary>Seçilən dövrdə qeydiyyatdan keçən yeni müştərilər.</summary>
        public int NewCustomers { get; set; }
        public int ActiveCards { get; set; }
        public int IosCards { get; set; }
        public int AndroidCards { get; set; }
        /// <summary>Filial üzrə bölgü (xala görə DESC, top 8).</summary>
        public List<BranchStatDto> BranchBreakdown { get; set; } = new();
    }

    public class PeriodStatDto
    {
        public DateOnly PeriodStart { get; set; }
        public int TransactionCount { get; set; }
        public decimal PointsEarned { get; set; }
        public decimal PointsSpent { get; set; }
        public int NewCustomers { get; set; }
    }

    public class PeriodTotalsDto
    {
        public int TotalTransactions { get; set; }
        public decimal TotalPointsEarned { get; set; }
        public decimal TotalPointsSpent { get; set; }
        public int NewCustomers { get; set; }
    }

    public class BranchStatDto
    {
        public int BranchId { get; set; }
        public string BranchName { get; set; } = "";
        public int TransactionCount { get; set; }
        public decimal PointsEarned { get; set; }
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

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

        // ── Dashboard v2.1 (müqayisə + müştəri bazası) ──
        /// <summary>Əvvəlki bərabər dövrün seriyası (eyni groupBy) — overlay müqayisə üçün.</summary>
        public List<PeriodStatDto> PreviousSeries { get; set; } = new();
        /// <summary>Tenant üzrə bütün aktiv müştərilər (dövrdən asılı deyil).</summary>
        public int TotalCustomers { get; set; }
        /// <summary>Mobil app-la qeydiyyatlı müştərilər (applicationUserId dolu).</summary>
        public int AppCustomers { get; set; }
        /// <summary>Wallet kartı olan müştərilər (IsIOS/IsAndroid kartı olan distinct customer).</summary>
        public int WalletCustomers { get; set; }

        // ── Dashboard v2.2 (istifadə göstəriciləri) ──
        // Ziyarət = bir kartın bir gündəki Applied tranzaksiyaları (kart × gün).
        /// <summary>Son 30 gündə ən azı bir tranzaksiyası olan kartlar (endDate-ə qədər).</summary>
        public int ActiveCards30d { get; set; }
        /// <summary>Ondan əvvəlki 30 gün üçün eyni göstərici (delta üçün).</summary>
        public int ActiveCards30dPrev { get; set; }
        /// <summary>Seçilən dövrdə gələn (tranzaksiyası olan) kartlar.</summary>
        public int EngagedCards { get; set; }
        /// <summary>Seçilən dövrdə ziyarətlərin cəmi (kart × gün).</summary>
        public int TotalVisits { get; set; }
        /// <summary>Seçilən dövrdə ≥2 ziyarəti olan kartlar.</summary>
        public int RepeatCards { get; set; }

        // ── Dashboard v2.3 (bildirişlər, 2026-10-06) ──
        /// <summary>Seçilən dövrdə tenant bildirişləri (manual + planlı + ad günü) və 7 günlük geri dönüş.</summary>
        public NotificationStatsDto? Notifications { get; set; }
    }

    public class NotificationStatsDto
    {
        /// <summary>Göndəriş sayı (uğurlu loglar).</summary>
        public int Sends { get; set; }
        public int BirthdaySends { get; set; }
        /// <summary>Alıcı cəmi (bütün göndərişlər).</summary>
        public int Recipients { get; set; }
        /// <summary>≥1 cihaza FCM çatan müştəri cəmi (v3 loglar).</summary>
        public int AppSent { get; set; }
        /// <summary>≥1 wallet kanalı uğurlu müştəri cəmi (v3 loglar).</summary>
        public int WalletSent { get; set; }
        /// <summary>Alıcı sətri olan (attribution mümkün) göndərişlərin alıcı cəmi — VisitRate məxrəci.</summary>
        public int AttributedRecipients { get; set; }
        /// <summary>Göndərişdən sonrakı AttributionDays gündə gələn (Earned/Redeemed) alıcılar.</summary>
        public int Visits { get; set; }
        /// <summary>Visits / AttributedRecipients × 100.</summary>
        public double VisitRate { get; set; }
        /// <summary>Gələn alıcıların Earned tx məbləği cəmi (₼).</summary>
        public decimal Revenue { get; set; }
        public int AttributionDays { get; set; } = 7;
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
        public int EngagedCards { get; set; }
        public int TotalVisits { get; set; }
        public int RepeatCards { get; set; }
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

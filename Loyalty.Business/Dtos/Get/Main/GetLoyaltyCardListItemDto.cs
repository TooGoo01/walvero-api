namespace Loyalty.Business.Dtos.Get.Main
{
    /// <summary>
    /// Customer list item-də istifadə olunan loyalty card DTO. Köhnə scanner build-i ilə geri uyğunluq üçün
    /// CurrentPoints/LifetimePoints int kimi serialize olunur (decimal → int truncate).
    /// </summary>
    public class GetLoyaltyCardListItemDto
    {
        public int Id { get; set; }
        public int CustomerId { get; set; }
        public int TenantId { get; set; }
        public int LoyaltyProgramId { get; set; }
        /// <summary>Bağlanmış kart (ambassador keçidi / silinmə) false — admin aktiv kartı seçir.</summary>
        public bool IsActive { get; set; }

        public string CardNumber { get; set; } = string.Empty;
        public string? BarcodeValue { get; set; }

        public decimal CurrentPoints { get; set; }
        public decimal LifetimePoints { get; set; }
        public int CurrentSteps { get; set; }
        public int CompletedCycles { get; set; }
        public int AvailableRewardCount { get; set; }
        public int MaxSpendCount { get; set; }
        public string? FreeRewardLabel { get; set; }
        public string? Currency { get; set; }

        /// <summary>Balansın son istifadə tarixi (UTC, ambassador). null = müddətsiz.</summary>
        public DateTime? PointsExpiresAt { get; set; }
        public bool IsPointsExpired { get; set; }

        // Tier %-ləri CardTemplate-dən kopyalanır (frontend tier-card bunları oxuyur)
        public int? BronzeThreshold { get; set; }
        public int? SilverThreshold { get; set; }
        public int? GoldThreshold { get; set; }
        public int? PlatinumThreshold { get; set; }
        public int? BronzePercent { get; set; }
        public int? SilverPercent { get; set; }
        public int? GoldPercent { get; set; }
        public int? PlatinumPercent { get; set; }
        public int? BronzePercentCash { get; set; }
        public int? SilverPercentCash { get; set; }
        public int? GoldPercentCash { get; set; }
        public int? PlatinumPercentCash { get; set; }
        public bool PayMentType { get; set; }
    }
}

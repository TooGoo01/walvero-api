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

        public string CardNumber { get; set; } = string.Empty;
        public string? BarcodeValue { get; set; }

        public int CurrentPoints { get; set; }
        public int LifetimePoints { get; set; }
        public int CurrentSteps { get; set; }
        public int CompletedCycles { get; set; }
        public int AvailableRewardCount { get; set; }
        public int MaxSpendCount { get; set; }
        public string? FreeRewardLabel { get; set; }
        public string? Currency { get; set; }
    }
}

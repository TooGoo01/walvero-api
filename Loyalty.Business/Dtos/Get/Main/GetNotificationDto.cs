namespace LoyaltyWalletApp.Business.Dtos.Get.Main
{
    public class GetNotificationDto
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Body { get; set; } = string.Empty;
        public bool IsRead { get; set; }
        public DateTime CreatedAt { get; set; }
        public int? RelatedFeedbackId { get; set; }
    }
}


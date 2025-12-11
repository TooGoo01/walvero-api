
using Loyalty.DataAccess.Enums;

namespace Loyalty.Business.Dtos.Get.Main
{
    public class GetStoreFeedbackDto
    {
        public int Id { get; set; }
        public int TenantId { get; set; }
        public string TenantName { get; set; } = string.Empty;
        public int CustomerId { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public string? CustomerPhoneNumber { get; set; } // Only included if SharePhoneNumber is true
        public FeedbackType Type { get; set; }
        public string Message { get; set; } = string.Empty;
        public double? Rating { get; set; }
        public bool SharePhoneNumber { get; set; }
        public FeedbackStatus Status { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public List<GetStoreFeedbackReplyDto> Replies { get; set; } = new();
    }
}


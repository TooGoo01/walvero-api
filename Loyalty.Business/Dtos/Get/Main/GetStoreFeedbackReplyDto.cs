

using Loyalty.DataAccess.Enums;

namespace Loyalty.Business.Dtos.Get.Main
{
    public class GetStoreFeedbackReplyDto
    {
        public int Id { get; set; }
        public int StoreFeedbackId { get; set; }
        public FeedbackReplySenderType SenderType { get; set; }
        public string Message { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
    }
}


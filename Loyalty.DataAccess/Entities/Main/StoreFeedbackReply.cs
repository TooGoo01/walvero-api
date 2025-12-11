

using Loyalty.DataAccess.Abstractions;
using Loyalty.DataAccess.Entities.Base;
using Loyalty.DataAccess.Enums;

namespace Loyalty.DataAccess.Entities.Main
{
    public class StoreFeedbackReply : EntityBase, IEntity
    {
        public int StoreFeedbackId { get; set; }
        public FeedbackReplySenderType SenderType { get; set; }
        public string Message { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation Properties
        public StoreFeedback StoreFeedback { get; set; } = null!;
    }
}


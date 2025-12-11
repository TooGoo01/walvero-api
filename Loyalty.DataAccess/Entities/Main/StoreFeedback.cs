

using Loyalty.DataAccess.Abstractions;
using Loyalty.DataAccess.Entities.Base;
using Loyalty.DataAccess.Enums;

namespace Loyalty.DataAccess.Entities.Main
{
    public class StoreFeedback : EntityBase, IEntity
    {
        public int TenantId { get; set; }
        public int CustomerId { get; set; }
        public FeedbackType Type { get; set; }
        public string Message { get; set; } = string.Empty;
        public double? Rating { get; set; } // 0-5, nullable
        public bool SharePhoneNumber { get; set; }
        public FeedbackStatus Status { get; set; } = FeedbackStatus.Open;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }

        // Navigation Properties
        public Tenant Tenant { get; set; } = null!;
        public Customer Customer { get; set; } = null!;
        public ICollection<StoreFeedbackReply> Replies { get; set; } = new List<StoreFeedbackReply>();
    }
}


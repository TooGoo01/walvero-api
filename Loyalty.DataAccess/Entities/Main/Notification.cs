using Loyalty.DataAccess.Abstractions;
using Loyalty.DataAccess.Entities.Base;
using Loyalty.DataAccess.Entities.Users;

namespace Loyalty.DataAccess.Entities.Main
{
    public class Notification : EntityBase, IEntity
    {
        public int UserId { get; set; } // FK to ApplicationUser (customer)
        public string Title { get; set; } = string.Empty;
        public string Body { get; set; } = string.Empty;
        public bool IsRead { get; set; } = false;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public int? RelatedFeedbackId { get; set; } // FK to StoreFeedback (optional)

        // Navigation Properties
        public ApplicationUser User { get; set; } = null!;
        public StoreFeedback? RelatedFeedback { get; set; }
    }
}


using Loyalty.DataAccess.Abstractions;
using Loyalty.DataAccess.Entities.Base;

namespace Loyalty.DataAccess.Entities.Sequences
{
    /// <summary>
    /// Tracks the last customer sequence number per tenant for CardNumber generation
    /// Uses atomic UPDATE...OUTPUT for concurrency-safe sequence generation
    /// </summary>
    public class TenantCustomerSequence : EntityBase, IEntity
    {
        public int TenantId { get; set; } // Primary Key
        public int LastNumber { get; set; } = 0; // Last sequence number used for this tenant
        public string Prefix { get; set; } = "789"; // Card number prefix (default 789)
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow; // Last update timestamp
        public byte[]? RowVersion { get; set; } // Optional row version for optimistic concurrency
    }
}


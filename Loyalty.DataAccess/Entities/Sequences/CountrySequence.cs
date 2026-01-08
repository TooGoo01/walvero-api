using Loyalty.DataAccess.Abstractions;
using Loyalty.DataAccess.Entities.Base;

namespace Loyalty.DataAccess.Entities.Sequences
{
    /// <summary>
    /// Tracks the last WalveroId sequence number per country code
    /// Uses atomic UPDATE...OUTPUT for concurrency-safe sequence generation
    /// </summary>
    public class CountrySequence : EntityBase, IEntity
    {
        public string? Name { get; set; } 
        public string? Code { get; set; }
        public string? Flag { get; set; } 
        public int CountryCode { get; set; } // Primary Key (e.g., 994, 336)
        public int LastNumber { get; set; } = 0; // Last sequence number used for this country
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow; // Last update timestamp
        public byte[]? RowVersion { get; set; } // Optional row version for optimistic concurrency
    }
}


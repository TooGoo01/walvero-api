using Loyalty.DataAccess.Abstractions;
using Loyalty.DataAccess.Entities.Base;

namespace Loyalty.DataAccess.Entities.Sequences
{
    /// <summary>
    /// Tracks the last WalveroId sequence number per country code
    /// </summary>
    public class CountrySequence : EntityBase,IEntity
    {
        public int CountryCode { get; set; } // Primary Key (e.g., 994, 336)
        public int LastNumber { get; set; } = 0; // Last sequence number used for this country
    }
}


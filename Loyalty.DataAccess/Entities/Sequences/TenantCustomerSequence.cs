using Loyalty.DataAccess.Abstractions;
using Loyalty.DataAccess.Entities.Base;

namespace Loyalty.DataAccess.Entities.Sequences
{
    /// <summary>
    /// Tracks the last customer sequence number per tenant for CardNumber generation
    /// </summary>
    public class TenantCustomerSequence :EntityBase, IEntity
    {
        public int TenantId { get; set; } // Primary Key
        public int LastNumber { get; set; } = 0; // Last sequence number used for this tenant
    }
}


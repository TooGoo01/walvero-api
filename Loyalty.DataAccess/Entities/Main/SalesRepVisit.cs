using Loyalty.DataAccess.Abstractions;
using Loyalty.DataAccess.Entities.Base;

namespace Loyalty.DataAccess.Entities.Main
{
    /// <summary>
    /// Satış mütəxəssisinin potensial müştəri məkanlarına etdiyi ziyarətlərin qeydi.
    /// </summary>
    public class SalesRepVisit : EntityBase, IEntity
    {
        public string? SalesRepName { get; set; }
        public string VenueName { get; set; } = string.Empty;
        public string? VenueAddress { get; set; }
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
        public string? ContactPhone { get; set; }
        public string? ContactName { get; set; }
        public string? Notes { get; set; }
        public SalesVisitStatus Status { get; set; } = SalesVisitStatus.Visited;
        public bool RemindFlag { get; set; }
        public DateTime? RemindAt { get; set; }
        public DateTime VisitDate { get; set; } = DateTime.UtcNow;
    }
}

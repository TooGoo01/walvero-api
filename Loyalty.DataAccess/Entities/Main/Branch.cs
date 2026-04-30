using Loyalty.DataAccess.Abstractions;
using Loyalty.DataAccess.Entities.Base;
using System.Collections.Generic;

namespace Loyalty.DataAccess.Entities.Main
{
    public class Branch : EntityBase, IEntity
    {
        public int TenantId { get; set; }
        public Tenant? Tenant { get; set; }

        public string Name { get; set; } = string.Empty;
        public string? Address { get; set; }
        public decimal? Latitude { get; set; }
        public decimal? Longitude { get; set; }
        public int RadiusMeters { get; set; } = 100;
        public string? Phone { get; set; }
        public string? Email { get; set; }
        public string? PhotoUrl { get; set; }
        public string? LogoUrl { get; set; }
        public string? WorkingHoursJson { get; set; }
        public bool IsMainBranch { get; set; } = false;

        // Optional contact / social links — same shape as Tenant
        public string? LocationUrl { get; set; }
        public string? WazeLocationUrl { get; set; }
        public string? MenuUrl { get; set; }
        public string? InstagramUrl { get; set; }
        public string? FacebookUrl { get; set; }
        public string? WhatsAppUrl { get; set; }
        public string? TelegramUrl { get; set; }
        public string? LinkedInUrl { get; set; }

        public ICollection<Room> Rooms { get; set; } = new List<Room>();
    }
}

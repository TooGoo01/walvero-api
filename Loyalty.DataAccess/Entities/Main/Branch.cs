using Loyalty.DataAccess.Abstractions;
using Loyalty.DataAccess.Entities.Base;
using Loyalty.DataAccess.Entities.Users;

namespace Loyalty.DataAccess.Entities.Main
{
    public class Branch : EntityBase, IEntity
    {
        public int TenantId { get; set; }
        public Tenant? Tenant { get; set; }

        public string Name { get; set; } = string.Empty;
        public string? Address { get; set; }
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
        public int RadiusMeters { get; set; } = 500;

        public string? Phone { get; set; }
        public string? Email { get; set; }
        public string? PhotoUrl { get; set; }

        /// <summary>
        /// JSON format: {"mon":"09:00-22:00","tue":"09:00-22:00",...,"sun":"10:00-20:00"}
        /// </summary>
        public string? WorkingHoursJson { get; set; }

        public bool IsMainBranch { get; set; } = false;

        // Navigation
        public ICollection<ApplicationUser> Staff { get; set; } = new List<ApplicationUser>();
        public ICollection<Transaction> Transactions { get; set; } = new List<Transaction>();
    }
}

using Loyalty.DataAccess.Abstractions;
using Loyalty.DataAccess.Entities.Base;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Loyalty.DataAccess.Entities.Main
{
    public class PassDeviceRegistration:EntityBase,IEntity
    {
        public string DeviceLibraryIdentifier { get; set; } = default!;
        public string PassTypeIdentifier { get; set; } = default!;
        public string SerialNumber { get; set; } = default!; // LoyaltyCard.AppleSerial ilə match

        public string PushToken { get; set; } = default!; // APNs device token
        public DateTime RegisteredAtUtc { get; set; } = DateTime.UtcNow;
        public DateTime? LastUpdatedUtc { get; set; }

        // Əlaqələr
        public int? LoyaltyCardId { get; set; } // serverdə serial → card mappinglə dolduracağıq
        public int? CustomerId { get; set; }
        public int TenantId { get; set; }

        // Audit
        public string? UserAgent { get; set; }
        public string? OsVersion { get; set; }
        public string? PassUrl { get; set; }
    }
}

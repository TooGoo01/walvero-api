using Loyalty.DataAccess.Abstractions;
using Loyalty.DataAccess.Entities.Base;
using Loyalty.DataAccess.Entities.Users;
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

        // Optional contact / social links � same shape as Tenant
        public string? LocationUrl { get; set; }
        public string? WazeLocationUrl { get; set; }
        public string? MenuUrl { get; set; }
        public string? InstagramUrl { get; set; }
        public string? FacebookUrl { get; set; }
        public string? WhatsAppUrl { get; set; }
        public string? TelegramUrl { get; set; }
        public string? LinkedInUrl { get; set; }
        public string? TiktokUrl { get; set; }

        // Branch-a aid brand renkleri. Boshdursa LoyaltyProgram CardTemplate-den fallback olunur.
        public string? BackgroundColor { get; set; }
        public string? ForegroundColor { get; set; }

        public ICollection<Room> Rooms { get; set; } = new List<Room>();

        // Inverse navigation collections — DbContext-də HasOne(...).WithMany(b => b.Staff/Transactions)
        // konfiqurasiyaları üçün lazımdır. ApplicationUser.BranchId və Transaction.BranchId nullable
        // FK-lər vasitəsilə bağlanır; bu kolleksiyalar ehtiyac olduqda Include ilə yüklənir.
        public ICollection<ApplicationUser> Staff { get; set; } = new List<ApplicationUser>();
        public ICollection<Transaction> Transactions { get; set; } = new List<Transaction>();
    }
}

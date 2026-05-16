
using Loyalty.DataAccess.Abstractions;
using Loyalty.DataAccess.Entities.Base;
using Loyalty.DataAccess.Entities.Main;
using Loyalty.DataAccess.Entities.Users;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Loyalty.DataAccess.Entities.Main
{
    public class Tenant : EntityBase, IEntity
    {

        public string CompanyName { get; set; } = string.Empty;
        public string Subdomain { get; set; } = string.Empty;
        public string ContactEmail { get; set; } = string.Empty;
        public string ContactPhone { get; set; } = string.Empty;
        public DateTime? SubscriptionExpiryDate { get; set; }
        public bool RequireConfirmation { get; set; } = true; // tenant setting
        public int ConfirmationTtlMinutes { get; set; } = 10;
        public bool AcceptAnonymus { get; set; } = false;
        public bool ShowOnHome { get; set; } = false;
        public bool IsHidden { get; set; } = false; // Hidden tenants (e.g., Walvero shadow tenant) never appear in listings
        public bool IsHasMenu { get; set; } = false; // Menyusu olan tenant-lar public siyahıda görünür

        // (Opsional) OTP kanal konfiqi
        public string? ConfirmationChannel { get; set; }
        public string? LocationUrl { get; set; }
        public string? InstagramUrl { get; set; } = string.Empty;
        public string? FacebookUrl { get; set; } = string.Empty;
        public string? WhatsAppUrl { get; set; } = string.Empty;
        public string? TeleqramUrl { get; set; } = string.Empty;
        public string? LinkedInUrl { get; set; } = string.Empty;
        public string? WazeLocationUrl { get; set; } = string.Empty;
        public string? TiktokUrl { get; set; } = string.Empty;
        public string? TripAdvisorUrl { get; set; } = string.Empty;
        public string? WebsiteUrl { get; set; } = string.Empty;
        public string? YoutubeUrl { get; set; } = string.Empty;
        public string? TwitterUrl { get; set; } = string.Empty;
        public string? YelpUrl { get; set; } = string.Empty;
        public string? ThreadsUrl { get; set; } = string.Empty;
        public string? PinterestUrl { get; set; } = string.Empty;

        public string? MenuUrl { get; set; } = string.Empty;

        // Navigation Properties
        public TenantLocation? TenantLocation { get; set; }
        public int? tenantLocationId { get; set; }
        public TenantBranding? Branding { get; set; }
        public int? categoryId { get; set; }
        public Category? Category { get; set; }
        public ICollection<ApplicationUser> Admins { get; set; } = new List<ApplicationUser>(); // YENİ
        public ICollection<Customer> Customers { get; set; } = new List<Customer>(); // YENİ (əvvəlki Users)
        public ICollection<LoyaltyProgram> LoyaltyPrograms { get; set; } = new List<LoyaltyProgram>();
        public ICollection<Branch> Branches { get; set; } = new List<Branch>();
        public ICollection<WalletCredential> WalletCredentials { get; set; } = new List<WalletCredential>();
        public ICollection<RestorantWifiInformation>? RestorantWifiInformation { get; set; } = new List<RestorantWifiInformation>();
        public ICollection<TenantLink>? Links { get; set; } = new List<TenantLink>();
        public ICollection<TenantIntegration> TenantIntegrations { get; set; } = new List<TenantIntegration>();
    }
}


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

        // (Opsional) OTP kanal konfiqi
        public string? ConfirmationChannel { get; set; }

        // Navigation Properties
       
        public TenantBranding? Branding { get; set; }
        public int? categoryId { get; set; }
        public Category? Category { get; set; }
        public ICollection<ApplicationUser> Admins { get; set; } = new List<ApplicationUser>(); // YENİ
        public ICollection<Customer> Customers { get; set; } = new List<Customer>(); // YENİ (əvvəlki Users)
        public ICollection<LoyaltyProgram> LoyaltyPrograms { get; set; } = new List<LoyaltyProgram>();
        public ICollection<WalletCredential> WalletCredentials { get; set; } = new List<WalletCredential>();
    }
}

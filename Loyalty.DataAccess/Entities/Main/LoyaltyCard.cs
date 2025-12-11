using Loyalty.DataAccess.Abstractions;
using Loyalty.DataAccess.Entities.Base;
using Loyalty.DataAccess.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Loyalty.DataAccess.Entities.Main
{
    public class LoyaltyCard : EntityBase, IEntity
    {
        public int CustomerId { get; set; } // YENİ (əvvəlki UserId)
        public int TenantId { get; set; }
        public int LoyaltyProgramId { get; set; }

        // Card Details
        public string CardNumber { get; set; } = string.Empty;
        public string? BarcodeValue { get; set; }
        public BarcodeType BarcodeType { get; set; } = BarcodeType.Code128;

        // Points
        public int CurrentPoints { get; set; } = 0;
        public int LifetimePoints { get; set; } = 0;
        public int CurrentSteps { get; set; } = 0;        // neçə qəhvə alınıb (məs: 7/10)
        public int CompletedCycles { get; set; } = 0;

        public bool IsAndroid { get; set; } = false;
        public bool IsIOS { get; set; } = false;
        public string? AppleSerial { get; set; }
        public string? ApplePassUrl { get; set; }
        public string? AppleAuthToken { get; set; }
        // Status
        public CardStatus Status { get; set; } = CardStatus.Active;
        public DateTime? ActivatedAt { get; set; }
        public DateTime? ExpiresAt { get; set; }

        // Navigation Properties
        public Customer Customer { get; set; } = null!; // YENİ
        public Tenant Tenant { get; set; } = null!;
        public LoyaltyProgram LoyaltyProgram { get; set; } = null!;
        public ICollection<WalletPass> WalletPasses { get; set; } = new List<WalletPass>();
        public ICollection<Transaction> Transactions { get; set; } = new List<Transaction>();
    }
}

using Loyalty.DataAccess.Enums;
using System.ComponentModel.DataAnnotations;

namespace Loyalty.Business.Dtos.Post.Main
{
    /// <summary>
    /// Kənar sistemdən (məs. PrimeHill) bir müştərinin köçürülməsi: müştəri + kart + açılış balansı.
    /// Kart nömrəsi Walvero generatoru ilə yaradılır; kənar kart nömrəsi yalnız istinaddır.
    /// </summary>
    public sealed class ImportCustomerDto
    {
        [Required] public int LoyaltyProgramId { get; set; }

        /// <summary>İstənilən format; servis "+994XXXXXXXXX"-ə çevirir.</summary>
        [Required, MaxLength(30)] public string Phone { get; set; } = string.Empty;

        [Required, MaxLength(100)] public string FirstName { get; set; } = string.Empty;
        [MaxLength(100)] public string? LastName { get; set; }
        [MaxLength(200)] public string? Email { get; set; }
        public Gender? Gender { get; set; }
        public DateTime? DateOfBirth { get; set; }

        /// <summary>Kənar sistemin kart nömrəsi — Transaction.ReferenceNumber-ə yazılır.</summary>
        [MaxLength(100)] public string? ExternalCardNumber { get; set; }

        /// <summary>Açılış balansı (xal). 0 → tranzaksiya yazılmır.</summary>
        public decimal Balance { get; set; }

        /// <summary>Idempotency açarı (Transaction.OrderId), məs. "primehill:105201". Eyni kartda təkrar → balans yazılmır.</summary>
        [MaxLength(100)] public string? OrderRef { get; set; }

        [MaxLength(200)] public string? Description { get; set; }
        public int? BranchId { get; set; }

        /// <summary>false → köçürülən müştəri xoş gəldin bonusu / hədiyyə almır (köhnə müştəri).</summary>
        public bool GrantWelcomeBonus { get; set; } = false;
    }

    public sealed class ImportCustomerResultDto
    {
        public int CustomerId { get; set; }
        public int LoyaltyCardId { get; set; }
        public string CardNumber { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public bool CustomerCreated { get; set; }
        public bool CardCreated { get; set; }
        public bool BalanceApplied { get; set; }
        public bool AlreadyImported { get; set; }
        public decimal BalanceBefore { get; set; }
        public decimal BalanceAfter { get; set; }
        public bool DryRun { get; set; }
        public List<string> Warnings { get; set; } = new();
    }
}

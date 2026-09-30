using System.ComponentModel.DataAnnotations;

namespace Loyalty.Business.Dtos.Post.Main
{
    /// <summary>
    /// Tenant admin tərəfindən ambassador (promoter) müştəri yaratmaq üçün.
    /// TenantId body-dən deyil, cari admin-in tenant-indən götürülür (controller).
    /// </summary>
    public sealed class CreateAmbassadorDto
    {
        [Required]
        public int LoyaltyProgramId { get; set; }

        [Required]
        [StringLength(50)]
        public string FirstName { get; set; } = "";

        [StringLength(50)]
        public string? LastName { get; set; }

        [Required]
        [Phone]
        public string PhoneNumber { get; set; } = "";

        [EmailAddress]
        public string? Email { get; set; }

        /// <summary>Ambassador-un fiziki kart nömrəsi (barcode). Boşdursa avtomatik generasiya.</summary>
        [StringLength(100)]
        public string? CardNumber { get; set; }

        /// <summary>Yaradılan anda karta yazılacaq ilkin xal (opsional).</summary>
        public decimal? InitialPoints { get; set; }

        /// <summary>Balansın son istifadə tarixi (UTC). Boş = müddətsiz. Keçəndə balans sıfırlanır.</summary>
        public DateTime? ExpiresAt { get; set; }
    }

    /// <summary>
    /// Mövcud müştərini ambassador et (+ opsional ilkin balans). Proqram göndərilməsə
    /// backend resolve edir (müştərinin kart proqramı → tenant-in ilk aktiv proqramı).
    /// </summary>
    public sealed class PromoteAmbassadorDto
    {
        [Required]
        public int CustomerId { get; set; }
        public int? LoyaltyProgramId { get; set; }
        /// <summary>Ambassador edilən anda karta yazılacaq ilkin balans/xal (opsional).</summary>
        public decimal? InitialPoints { get; set; }
        /// <summary>Balansın son istifadə tarixi (UTC). Boş = müddətsiz.</summary>
        public DateTime? ExpiresAt { get; set; }
    }

    /// <summary>
    /// Mövcud karta manual xal (balans) əlavə etmək — ambassador-a. Wallet push + POS forward tetiklenir.
    /// </summary>
    public sealed class CreditPointsDto
    {
        [Required]
        public int LoyaltyCardId { get; set; }

        [Required]
        public decimal Points { get; set; }

        [StringLength(200)]
        public string? Description { get; set; }

        /// <summary>
        /// Balansın son istifadə tarixi (UTC). Verilibsə kartın tarixini əvəz edir; verilməyibsə mövcud tarix qalır
        /// (keçmişdirsə təmizlənir ki, yeni xal dərhal sıfırlanmasın).
        /// </summary>
        public DateTime? ExpiresAt { get; set; }
    }

    /// <summary>Kart balansının son istifadə tarixini dəyiş / sil (xal vermədən). null = müddətsiz. Keçmiş tarix → dərhal sıfırlama.</summary>
    public sealed class SetPointsExpiryDto
    {
        [Required]
        public int LoyaltyCardId { get; set; }
        public DateTime? ExpiresAt { get; set; }
    }
}

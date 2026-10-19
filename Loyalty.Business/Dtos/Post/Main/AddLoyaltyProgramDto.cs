using Loyalty.DataAccess.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Loyalty.Business.Dtos.Post.Main
{
    public class AddLoyaltyProgramDto
    {
        public int? TenantId { get; set; }                 // boş gələrsə ITenantProvider-dən götürəcəyik
        public string ProgramName { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? Code { get; set; }                   // boşdursa ProgramName-dən generasiya
        public decimal PointsPerCurrency { get; set; } = 1; // 1 AZN = X point
        public decimal CurrencyPerPoint { get; set; } = 0.1m;
        public LoyaltyProgramType ProgramType { get; set; } = LoyaltyProgramType.ProgressBased;
        public int MinPointsToRedeem { get; set; } = 0;
        public int? PointsExpiryDays { get; set; }
        public bool IsActive { get; set; } = true;

        // Lottery
        public bool IsLotteryEnabled { get; set; } = false;
        public int LotteryPurchaseThreshold { get; set; } = 5;
        public decimal? LotteryAmountThreshold { get; set; }

        // HappyHours base rates (window xaricində)
        public int? BasePercentCash { get; set; }
        public int? BasePercentCard { get; set; }
        public List<HappyHoursWindowDto>? HappyHoursWindows { get; set; }

        // null → yeni proqramda Public, mövcudda dəyişmir (köhnə admin formaları sahəni göndərmir)
        public ProgramAudience? Audience { get; set; }

        // null → yeni proqramda Legacy, mövcudda dəyişmir
        public PosEarnSource? PosEarnSource { get; set; }

        // Möhür proqramında qayda uyğun gəlməyən POS sifarişinə möhür sayı. null = dəyişmə; 0 = söndür
        public int? StepsPerOrder { get; set; }

        // iiko cüzdanı / onun iiko proqramı (GUID). null = dəyişmə; "" = sil
        public string? PosWalletId { get; set; }
        public string? PosExternalProgramId { get; set; }
    }

    /// <summary>Proqramın POS (iiko) bağlantısı — tenant admin öz proqramı üçün dəyişir. null sahə = dəyişmə.</summary>
    /// <summary>Tenant admin: ambassador kartı yarat (dizayn adi kartdan kopyalanır).</summary>
    public class CreateAmbassadorProgramDto
    {
        /// <summary>1 = ₼, 2 = məhsul sayı, 3 = möhür</summary>
        public Loyalty.Business.Helpers.AmbassadorCardKind Kind { get; set; } = Loyalty.Business.Helpers.AmbassadorCardKind.Money;
        /// <summary>Möhür kartı üçün hədiyyəyə qədər möhür sayı (default 6).</summary>
        public int? TotalSteps { get; set; }
        public string? Name { get; set; }
    }

    public class UpdatePosLinkDto
    {
        public PosEarnSource? PosEarnSource { get; set; }
        /// <summary>0 = söndür</summary>
        public int? StepsPerOrder { get; set; }
        /// <summary>"" = sil</summary>
        public string? PosWalletId { get; set; }
        /// <summary>"" = sil</summary>
        public string? PosExternalProgramId { get; set; }
    }
}

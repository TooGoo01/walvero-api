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
    public class LoyaltyProgram : EntityBase, IEntity
    {
        public int TenantId { get; set; }
            
        public string ProgramName { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string Code { get; set; } = string.Empty;

        // Points Configuration
        public decimal PointsPerCurrency { get; set; } = 1; // məs: 1 AZN = 10 xal
        public decimal CurrencyPerPoint { get; set; } = 0.1m; // məs: 1 xal = 0.10 AZN
        public LoyaltyProgramType ProgramType { get; set; } = LoyaltyProgramType.ProgressBased;

        // Rules
        public int MinPointsToRedeem { get; set; } = 0;
        public int? PointsExpiryDays { get; set; } // null = heç vaxt bitmir
        public bool IsActive { get; set; } = true;
        public int? CardTemplateId { get; set; }
        public CardTemplate? CardTemplate { get; set; }

        // Lottery Configuration
        public bool IsLotteryEnabled { get; set; } = false;
        public int LotteryPurchaseThreshold { get; set; } = 5;
        public decimal? LotteryAmountThreshold { get; set; }

        // HappyHours base rates (window xaricində istifadə olunur)
        public int? BasePercentCash { get; set; }
        public int? BasePercentCard { get; set; }

        // Xoş gəldin bonusu — kartın ilk Applied qazancında (1 dəfə)
        public bool FirstPurchaseBonusEnabled { get; set; } = false;
        public WelcomeBonusType FirstPurchaseBonusType { get; set; } = WelcomeBonusType.Percent;
        public decimal FirstPurchaseBonusValue { get; set; } = 0;
        public decimal? FirstPurchaseBonusMaxPoints { get; set; }     // yalnız Percent üçün tavan

        // Kart yaradılanda hədiyyə (1 dəfə)
        public bool SignupGiftEnabled { get; set; } = false;
        public SignupGiftType SignupGiftType { get; set; } = SignupGiftType.Points;
        public int SignupGiftValue { get; set; } = 0;

        // Ambassador seqmenti: Ambassador proqramı discover/home/public qeydiyyatda görünmür,
        // kartı yalnız AmbassadorService açır. Tenant başına ən çox bir aktiv Ambassador proqramı.
        public ProgramAudience Audience { get; set; } = ProgramAudience.Public;

        // POS (iiko) qazanc mənbəyi — bax PosEarnSource; Legacy = bugünkü davranış
        public PosEarnSource PosEarnSource { get; set; } = PosEarnSource.Legacy;

        // Möhür (ProgressBased) proqramında məhsul qaydası uyğun gəlməyən hər POS sifarişinə verilən möhür sayı; null = yox
        public int? StepsPerOrder { get; set; }

        // iiko cüzdanı (GUID) — bu proqramın kartlarının balansı iiko-da hansı cüzdandadır (məs. pims: Cashback vs Ambassador).
        // null = bugünkü davranış (müştərinin birinci cüzdanı). PosExternalProgramId — həmin cüzdanın iiko proqramı
        // (müştəridə cüzdan yoxdursa proqrama yazılır ki, cüzdan yaransın).
        public string? PosWalletId { get; set; }
        public string? PosExternalProgramId { get; set; }

        // Navigation Properties
        public Tenant Tenant { get; set; } = null!;
        public ICollection<LoyaltyCard> LoyaltyCards { get; set; } = new List<LoyaltyCard>();
        public ICollection<Reward> Rewards { get; set; } = new List<Reward>();
        public ICollection<LotteryDraw> LotteryDraws { get; set; } = new List<LotteryDraw>();
        public ICollection<HappyHoursWindow> HappyHoursWindows { get; set; } = new List<HappyHoursWindow>();
    }
}

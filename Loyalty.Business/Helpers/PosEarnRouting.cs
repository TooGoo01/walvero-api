using Loyalty.DataAccess.Entities.Main;
using Loyalty.DataAccess.Enums;

namespace Loyalty.Business.Helpers
{
    /// <summary>
    /// POS (iiko / Monyo) qazanc qərarları — kartın proqramına görə (DB-siz, test olunur).
    /// </summary>
    public static class PosEarnRouting
    {
        /// <summary>
        /// Kartın proqramına aid məhsul qaydaları (açar = ExternalProductId): proqram-spesifik qayda ümumidən
        /// (LoyaltyProgramId null) üstündür; başqa proqramın qaydası düşür. Dublikat açar atmır.
        /// </summary>
        public static Dictionary<string, PosProductEarnRule> RulesForProgram(IEnumerable<PosProductEarnRule> rules, int programId)
            => rules
                .Where(r => !string.IsNullOrWhiteSpace(r.ExternalProductId)
                            && (r.LoyaltyProgramId == null || r.LoyaltyProgramId == programId))
                .GroupBy(r => r.ExternalProductId.Trim(), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    g => g.Key,
                    g => g.OrderByDescending(r => r.LoyaltyProgramId.HasValue).ThenByDescending(r => r.Id).First(),
                    StringComparer.OrdinalIgnoreCase);

        /// <summary>OrderClosed-da Walvero qazancı hesablasınmı (PosMirror-da iiko hesablayır).</summary>
        public static bool ComputeOrderEarn(PosEarnSource source) => source != PosEarnSource.PosMirror;

        /// <summary>OrderClosed-da iiko cüzdan ödənişi balansdan çıxılsınmı (PosMirror-da güzgü gətirir — ikiqat çıxılmasın).</summary>
        public static bool ApplyOrderWalletPayment(PosEarnSource source) => source != PosEarnSource.PosMirror;

        /// <summary>iiko cüzdan güzgüsü (iiko_webhook) kart balansını yazsınmı (WalveroRules-da yox — yalnız marker).</summary>
        public static bool MirrorWritesBalance(PosEarnSource source) => source != PosEarnSource.WalveroRules;

        /// <summary>OrderClosed qazancından sonra yeni balans iiko cüzdanına göndərilsinmi (yalnız WalveroRules).</summary>
        public static bool PushBalanceAfterOrderEarn(PosEarnSource source) => source == PosEarnSource.WalveroRules;

        /// <summary>
        /// Möhür proqramında heç bir məhsul qaydası uyğun gəlmədikdə sifariş başına möhür (LoyaltyProgram.StepsPerOrder).
        /// </summary>
        public static int StepsForOrder(int? stepsPerOrder, bool anyRuleMatched, CardTemplateType effectiveType)
            => !anyRuleMatched && effectiveType == CardTemplateType.ProgressBased && stepsPerOrder is > 0
                ? stepsPerOrder.Value
                : 0;
    }
}

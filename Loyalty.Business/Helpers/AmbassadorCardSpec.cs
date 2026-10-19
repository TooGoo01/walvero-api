using Loyalty.DataAccess.Enums;

namespace Loyalty.Business.Helpers
{
    /// <summary>Ambassador kartının balans növü (yaradanda seçilir; DB-də saxlanmır — şablon tipi + valyuta ilə ifadə olunur).</summary>
    public enum AmbassadorCardKind
    {
        /// <summary>₼ balans (adi xal/keşbek kartı kimi)</summary>
        Money = 1,
        /// <summary>Balans = pulsuz məhsul sayı (valyuta «məhsul»)</summary>
        Units = 2,
        /// <summary>Möhür kartı (N möhür → hədiyyə)</summary>
        Stamps = 3
    }

    /// <summary>Balans növündən proqram/şablon parametrləri. Pure — test olunur.</summary>
    public sealed record AmbassadorCardSpec(
        LoyaltyProgramType ProgramType,
        CardTemplateType TemplateType,
        string? Currency,
        int? TotalSteps,
        PosEarnSource EarnSource)
    {
        public const int DefaultTotalSteps = 6;
        public const int MaxTotalSteps = 50;

        /// <param name="sourceCurrency">Adi kartın valyutası (₼ kartı üçün saxlanır).</param>
        /// <param name="earnSource">Verilməsə: möhür kartı → WalveroRules (kassa güzgüsü möhürləri pozmasın), qalanları → Legacy.</param>
        public static AmbassadorCardSpec For(AmbassadorCardKind kind, int? totalSteps, string? sourceCurrency, PosEarnSource? earnSource = null)
            => kind switch
            {
                AmbassadorCardKind.Units => new(LoyaltyProgramType.PointsBased, CardTemplateType.PointsBased,
                    IikoImportPlanner.UnitCurrency, null, earnSource ?? PosEarnSource.Legacy),
                AmbassadorCardKind.Stamps => new(LoyaltyProgramType.ProgressBased, CardTemplateType.ProgressBased,
                    null, Math.Clamp(totalSteps ?? DefaultTotalSteps, 1, MaxTotalSteps), earnSource ?? PosEarnSource.WalveroRules),
                _ => new(LoyaltyProgramType.PointsBased, CardTemplateType.PointsBased,
                    string.IsNullOrWhiteSpace(sourceCurrency) || sourceCurrency == IikoImportPlanner.UnitCurrency ? "AZN" : sourceCurrency,
                    null, earnSource ?? PosEarnSource.Legacy),
            };
    }
}

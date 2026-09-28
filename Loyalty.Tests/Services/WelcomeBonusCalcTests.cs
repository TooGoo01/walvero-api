using FluentAssertions;
using Loyalty.Business.Services.Implementations.Main;
using Loyalty.DataAccess.Enums;

namespace Loyalty.Tests.Services;

public class WelcomeBonusCalcTests
{
    private static decimal Calc(
        WelcomeBonusType type, decimal value, decimal? cap,
        CardTemplateType template, decimal? amount, decimal regular) =>
        WelcomeBonusService.CalculateFirstPurchaseBonus(type, value, cap, template, amount, regular);

    [Fact]
    public void Percent_PointsBased_TopsUpToTotalPercent()
    {
        // 7 AZN, proqram 5% (0.35 verilib), bonus 20% → cəmi 1.40 → top-up 1.05
        Calc(WelcomeBonusType.Percent, 20, null, CardTemplateType.PointsBased, 7m, 0.35m)
            .Should().Be(1.05m);
    }

    [Fact]
    public void Percent_PointsBased_RegularAlreadyAboveTarget_ReturnsZero()
    {
        // Tier 25% (1.75) > hədəf 20% (1.40) → bonus yoxdur
        Calc(WelcomeBonusType.Percent, 20, null, CardTemplateType.PointsBased, 7m, 1.75m)
            .Should().BeLessThanOrEqualTo(0m);
    }

    [Fact]
    public void Percent_PointsBased_CapAppliesToTopUp()
    {
        // 100 AZN, hədəf 20, adi 5 → top-up 15, tavan 10 → 10
        Calc(WelcomeBonusType.Percent, 20, 10m, CardTemplateType.PointsBased, 100m, 5m)
            .Should().Be(10m);
    }

    [Fact]
    public void Percent_ProgressBased_FloorsAndIgnoresRegular()
    {
        // Ştamp kartı: floor(7 × 20%) = 1, adi qazanc (1 ştamp) çıxılmır
        Calc(WelcomeBonusType.Percent, 20, null, CardTemplateType.ProgressBased, 7m, 1m)
            .Should().Be(1m);
    }

    [Fact]
    public void Fixed_IsAdditive_IgnoresRegular()
    {
        Calc(WelcomeBonusType.Fixed, 5, null, CardTemplateType.PointsBased, 7m, 0.35m)
            .Should().Be(5m);
    }

    [Fact]
    public void Percent_NoAmount_ReturnsZero()
    {
        Calc(WelcomeBonusType.Percent, 20, null, CardTemplateType.PointsBased, null, 0m)
            .Should().Be(0m);
    }
}

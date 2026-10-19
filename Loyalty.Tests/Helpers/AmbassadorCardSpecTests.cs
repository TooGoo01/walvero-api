using FluentAssertions;
using Loyalty.Business.Helpers;
using Loyalty.DataAccess.Enums;

namespace Loyalty.Tests.Helpers;

/// <summary>Ambassador kartının balans növü → proqram/şablon parametrləri.</summary>
public class AmbassadorCardSpecTests
{
    [Fact]
    public void Money_KeepsTenantCurrency_AndLegacySource()
    {
        var s = AmbassadorCardSpec.For(AmbassadorCardKind.Money, null, "AZN");
        s.TemplateType.Should().Be(CardTemplateType.PointsBased);
        s.ProgramType.Should().Be(LoyaltyProgramType.PointsBased);
        s.Currency.Should().Be("AZN");
        s.TotalSteps.Should().BeNull();
        s.EarnSource.Should().Be(PosEarnSource.Legacy);
    }

    [Fact]
    public void Money_FromUnitsSource_FallsBackToAzn()
        => AmbassadorCardSpec.For(AmbassadorCardKind.Money, null, IikoImportPlanner.UnitCurrency).Currency.Should().Be("AZN");

    [Fact]
    public void Units_IsPointsCardWithUnitCurrency()
    {
        var s = AmbassadorCardSpec.For(AmbassadorCardKind.Units, 9, "AZN");
        s.TemplateType.Should().Be(CardTemplateType.PointsBased);
        s.Currency.Should().Be(IikoImportPlanner.UnitCurrency);
        s.TotalSteps.Should().BeNull();
    }

    [Theory]
    [InlineData(null, AmbassadorCardSpec.DefaultTotalSteps)]
    [InlineData(10, 10)]
    [InlineData(0, 1)]
    [InlineData(500, AmbassadorCardSpec.MaxTotalSteps)]
    public void Stamps_ClampsSteps_AndWalveroComputes(int? input, int expected)
    {
        var s = AmbassadorCardSpec.For(AmbassadorCardKind.Stamps, input, "AZN");
        s.TemplateType.Should().Be(CardTemplateType.ProgressBased);
        s.ProgramType.Should().Be(LoyaltyProgramType.ProgressBased);
        s.TotalSteps.Should().Be(expected);
        s.EarnSource.Should().Be(PosEarnSource.WalveroRules); // kassa güzgüsü möhürləri pozmasın
    }

    [Fact]
    public void ExplicitEarnSource_Wins()
        => AmbassadorCardSpec.For(AmbassadorCardKind.Units, null, null, PosEarnSource.PosMirror).EarnSource.Should().Be(PosEarnSource.PosMirror);
}

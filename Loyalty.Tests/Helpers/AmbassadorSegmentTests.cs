using FluentAssertions;
using Loyalty.Business.Helpers;
using Loyalty.DataAccess.Entities.Main;
using Loyalty.DataAccess.Enums;

namespace Loyalty.Tests.Helpers;

/// <summary>Ambassador seqmenti — kart keçidi qaydaları və default proqram seçimi.</summary>
public class AmbassadorSegmentTests
{
    [Theory]
    [InlineData(CardTemplateType.PointsBased, CardTemplateType.PointsBased, true)]
    [InlineData(CardTemplateType.TierBased, CardTemplateType.PointsBased, true)]
    [InlineData(CardTemplateType.PointsBased, CardTemplateType.ProgressBased, false)]
    [InlineData(CardTemplateType.ProgressBased, CardTemplateType.PointsBased, false)]
    [InlineData(CardTemplateType.ProgressBased, CardTemplateType.ProgressBased, false)]
    public void CanCarryOver_OnlyBetweenMoneyCards(CardTemplateType from, CardTemplateType to, bool expected)
        => AmbassadorCardSwitch.CanCarryOver(from, to).Should().Be(expected);

    [Fact]
    public void CardNumberCandidate_AddsSuffixThenCounter()
    {
        AmbassadorCardSwitch.CardNumberCandidate(" 789000000001 ", "A", 0).Should().Be("789000000001-A");
        AmbassadorCardSwitch.CardNumberCandidate("789000000001", "A", 1).Should().Be("789000000001-A2");
        AmbassadorCardSwitch.CardNumberCandidate("789000000001", "P", 2).Should().Be("789000000001-P3");
    }

    [Fact]
    public void CardNumberCandidate_FitsMaxLengthKeepingSuffix()
    {
        var c = AmbassadorCardSwitch.CardNumberCandidate(new string('9', 60), "A", 11);
        c.Length.Should().Be(AmbassadorCardSwitch.MaxCardNumberLength);
        c.Should().EndWith("-A12");
    }

    private static LoyaltyProgram P(int id, bool active = true, ProgramAudience aud = ProgramAudience.Public, bool template = true)
        => new() { Id = id, IsActive = active, Audience = aud, CardTemplate = template ? new CardTemplate() : null };

    [Fact]
    public void DefaultProgram_SkipsAmbassadorAndInactive_OrdersById()
    {
        var programs = new[]
        {
            P(5, aud: ProgramAudience.Ambassador),
            P(9),
            P(3, active: false),
            P(7),
        };
        programs.DefaultProgram()!.Id.Should().Be(7);
        programs.ActivePublic().Select(p => p.Id).Should().Equal(7, 9);
    }

    [Fact]
    public void DefaultProgramWithTemplate_RequiresTemplate()
    {
        var programs = new[] { P(2, template: false), P(4) };
        programs.DefaultProgramWithTemplate()!.Id.Should().Be(4);
        ((IEnumerable<LoyaltyProgram>?)null).DefaultProgram().Should().BeNull();
    }

    [Fact]
    public void DefaultProgram_OnlyAmbassador_ReturnsNull()
        => new[] { P(1, aud: ProgramAudience.Ambassador) }.DefaultProgram().Should().BeNull();
}

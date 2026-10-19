using FluentAssertions;
using Loyalty.Business.Helpers;
using Loyalty.DataAccess.Entities.Main;
using Loyalty.DataAccess.Enums;

namespace Loyalty.Tests.Helpers;

/// <summary>POS qazanc qərarları — proqrama bağlı məhsul qaydaları, PosEarnSource cədvəli, sifariş başına möhür.</summary>
public class PosEarnRoutingTests
{
    private static PosProductEarnRule R(int id, string product, int? programId, int steps = 1)
        => new() { Id = id, ExternalProductId = product, LoyaltyProgramId = programId, StepsPerUnit = steps };

    [Fact]
    public void RulesForProgram_ProgramSpecificBeatsGeneral_OtherProgramDropped()
    {
        var rules = new[]
        {
            R(1, "COFFEE", null, steps: 1),
            R(2, "coffee ", 20, steps: 3),   // ambassador proqramı (20) üçün override, açar trim + case-insensitive
            R(3, "TEA", 99, steps: 5),       // başqa proqram
            R(4, "CAKE", null, steps: 2),
        };

        var forAmbassador = PosEarnRouting.RulesForProgram(rules, 20);
        forAmbassador["COFFEE"].StepsPerUnit.Should().Be(3);
        forAmbassador.Should().NotContainKey("TEA");
        forAmbassador["CAKE"].StepsPerUnit.Should().Be(2);

        var forPublic = PosEarnRouting.RulesForProgram(rules, 10);
        forPublic["COFFEE"].StepsPerUnit.Should().Be(1);
        forPublic.Should().HaveCount(2);
    }

    [Fact]
    public void RulesForProgram_DuplicateGeneralRules_DoNotThrow_NewestWins()
    {
        var rules = new[] { R(1, "X", null, 1), R(5, "X", null, 4) };
        PosEarnRouting.RulesForProgram(rules, 1)["X"].StepsPerUnit.Should().Be(4);
    }

    [Theory]
    //                                   computeEarn walletPay mirrorWrites pushAfterEarn
    [InlineData(PosEarnSource.Legacy,       true,      true,     true,        false)]
    [InlineData(PosEarnSource.WalveroRules, true,      true,     false,       true)]
    [InlineData(PosEarnSource.PosMirror,    false,     false,    true,        false)]
    public void EarnSourceTable(PosEarnSource source, bool computeEarn, bool walletPay, bool mirrorWrites, bool pushAfterEarn)
    {
        PosEarnRouting.ComputeOrderEarn(source).Should().Be(computeEarn);
        PosEarnRouting.ApplyOrderWalletPayment(source).Should().Be(walletPay);
        PosEarnRouting.MirrorWritesBalance(source).Should().Be(mirrorWrites);
        PosEarnRouting.PushBalanceAfterOrderEarn(source).Should().Be(pushAfterEarn);
    }

    [Theory]
    [InlineData(1, false, CardTemplateType.ProgressBased, 1)]
    [InlineData(2, false, CardTemplateType.ProgressBased, 2)]
    [InlineData(1, true, CardTemplateType.ProgressBased, 0)]   // məhsul qaydası uyğun gəlib — sifariş möhürü yox
    [InlineData(1, false, CardTemplateType.PointsBased, 0)]    // xal kartında möhür yoxdur
    [InlineData(null, false, CardTemplateType.ProgressBased, 0)]
    [InlineData(0, false, CardTemplateType.ProgressBased, 0)]
    public void StepsForOrder(int? stepsPerOrder, bool ruleMatched, CardTemplateType type, int expected)
        => PosEarnRouting.StepsForOrder(stepsPerOrder, ruleMatched, type).Should().Be(expected);
}

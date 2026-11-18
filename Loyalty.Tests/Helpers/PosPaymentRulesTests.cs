using FluentAssertions;
using Loyalty.Business.Helpers;
using Loyalty.Business.Services.Abstractions.Main;
using Xunit;

namespace Loyalty.Tests.Helpers;

public class PosPaymentRulesTests
{
    [Theory]
    [InlineData("Compliment", true)]
    [InlineData(" compliment ", true)]
    [InlineData("COMPLIMENT", true)]
    [InlineData("Kompliment", true)]
    [InlineData("Комплимент", true)]
    [InlineData("Cash", false)]
    [InlineData("Card", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsCompliment_MatchesByName(string? name, bool expected)
        => PosPaymentRules.IsCompliment(name).Should().Be(expected);

    [Fact]
    public void ComplimentSum_MixedPayments_CountsOnlyCompliment()
    {
        var payments = new List<IikoOrderPayment>
        {
            new() { PaymentTypeName = "Cash", Sum = 10m },
            new() { PaymentTypeName = "Compliment", Sum = 6.5m },
        };
        PosPaymentRules.ComplimentSum(payments).Should().Be(6.5m);
    }

    [Fact]
    public void ComplimentSum_IgnoresWalletPaymentWithCredential()
    {
        var payments = new List<IikoOrderPayment>
        {
            new() { PaymentTypeName = "Compliment", Sum = 5m, Credential = "101211" },
            new() { PaymentTypeName = "Compliment", Sum = 9.5m },
        };
        PosPaymentRules.ComplimentSum(payments).Should().Be(9.5m);
    }

    [Fact]
    public void ComplimentSum_NullOrEmpty_IsZero()
    {
        PosPaymentRules.ComplimentSum(null).Should().Be(0m);
        PosPaymentRules.ComplimentSum(new List<IikoOrderPayment>()).Should().Be(0m);
    }
}

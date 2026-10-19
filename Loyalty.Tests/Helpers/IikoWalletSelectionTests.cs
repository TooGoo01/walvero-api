using FluentAssertions;
using Loyalty.Business.Helpers;

namespace Loyalty.Tests.Helpers;

/// <summary>iiko cüzdan seçimi — pims: Cashback + Ambassador cüzdanı; proqramın cüzdanı başqasına düşməməlidir.</summary>
public class IikoWalletSelectionTests
{
    private static readonly Guid Cashback = Guid.Parse("d0b10000-8bc3-62f6-adf1-08df1ee98316");
    private static readonly Guid Ambassador = Guid.Parse("7dd50000-64fa-c2b3-6e83-08df2c4fda0c");

    private static readonly List<(Guid Id, decimal Balance)> Both = new() { (Cashback, 11.05m), (Ambassador, 3m) };

    [Fact]
    public void NoPreferred_FirstWallet_TodayBehaviour()
    {
        var r = IikoWalletSelection.Pick(Both, null);
        r.Outcome.Should().Be(IikoWalletSelection.Outcome.Found);
        r.WalletId.Should().Be(Cashback);
        r.Balance.Should().Be(11.05m);
    }

    [Fact]
    public void Preferred_PicksThatWallet_NotFirst()
    {
        var r = IikoWalletSelection.Pick(Both, Ambassador.ToString().ToUpperInvariant());
        r.Outcome.Should().Be(IikoWalletSelection.Outcome.Found);
        r.WalletId.Should().Be(Ambassador);
        r.Balance.Should().Be(3m);
    }

    [Fact]
    public void PreferredMissing_DoesNotFallBackToOtherWallet()
    {
        var r = IikoWalletSelection.Pick(new List<(Guid, decimal)> { (Cashback, 11.05m) }, Ambassador.ToString());
        r.Outcome.Should().Be(IikoWalletSelection.Outcome.PreferredMissing);
        r.WalletId.Should().Be(Ambassador);
    }

    [Fact]
    public void NoWallets()
        => IikoWalletSelection.Pick(new List<(Guid, decimal)>(), null).Outcome.Should().Be(IikoWalletSelection.Outcome.NoWallets);

    [Theory]
    [InlineData(null, "anything", true)]                                         // cüzdan təyin edilməyib — bugünkü kimi
    [InlineData("7dd50000-64fa-c2b3-6e83-08df2c4fda0c", "7DD50000-64FA-C2B3-6E83-08DF2C4FDA0C", true)]
    [InlineData("7dd50000-64fa-c2b3-6e83-08df2c4fda0c", "d0b10000-8bc3-62f6-adf1-08df1ee98316", false)] // keşbek cüzdanı → ambassador kartına yazılmasın
    [InlineData("7dd50000-64fa-c2b3-6e83-08df2c4fda0c", null, false)]
    public void MirrorAppliesToCard(string? programWallet, string? txWallet, bool expected)
        => IikoWalletSelection.MirrorAppliesToCard(programWallet, txWallet).Should().Be(expected);
}

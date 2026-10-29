using FluentAssertions;
using Loyalty.Business.Helpers;

namespace Loyalty.Tests.Helpers;

/// <summary>Google Wallet şəkil URL-i: boşluq / qeyri-ASCII fayl adı API-də 400 "Invalid URL" verirdi (Pims loqosu, 2026-10-29).</summary>
public class WalletUrlTests
{
    [Fact]
    public void Safe_EncodesSpacesAndNonAscii()
        => WalletUrl.Safe("https://cdn.walvero.com/tenant-56-program-52-template/Adsız tasarım (59)883.png")
            .Should().Be("https://cdn.walvero.com/tenant-56-program-52-template/Ads%C4%B1z%20tasar%C4%B1m%20(59)883.png");

    [Fact]
    public void Safe_AlreadyEncodedOrPlain_Unchanged()
    {
        WalletUrl.Safe("https://cdn.walvero.com/a/Ads%C4%B1z%20x.png").Should().Be("https://cdn.walvero.com/a/Ads%C4%B1z%20x.png");
        WalletUrl.Safe("https://cdn.walvero.com/a/logo.png").Should().Be("https://cdn.walvero.com/a/logo.png");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not a url")]
    public void Safe_InvalidOrEmpty_Passthrough(string? url) => WalletUrl.Safe(url).Should().Be(url);
}

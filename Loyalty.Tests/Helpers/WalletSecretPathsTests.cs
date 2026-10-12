using FluentAssertions;
using Loyalty.Business.Helpers;

namespace Loyalty.Tests.Helpers;

/// <summary>Wallet sirr linklərinin (öz blob/CDN) parse-i və parol markeri.</summary>
public class WalletSecretPathsTests
{
    [Theory]
    [InlineData("https://walverostorage.blob.core.windows.net/tenant-16-wallet-google/key123.json", "tenant-16-wallet-google", "key123.json")]
    [InlineData("https://cdn.walvero.com/tenant-56-wallet-apple/cafedraWalletCert7.p12", "tenant-56-wallet-apple", "cafedraWalletCert7.p12")]
    [InlineData("https://WALVEROSTORAGE.blob.core.windows.net/c/a%20b.p12?sv=x&sig=y", "c", "a b.p12")]
    [InlineData("https://walverostorage.blob.core.windows.net/c/dir/sub/file.cer", "c", "dir/sub/file.cer")]
    public void OwnBlobUrl_Parsed(string url, string container, string blob)
    {
        WalletSecretPaths.TryParseOwnBlobUrl(url, out var c, out var b).Should().BeTrue();
        c.Should().Be(container);
        b.Should().Be(blob);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("/resource/tenant-1/wallet/apple/7gulCert.p12")]
    [InlineData("https://appapi.walvero.com/resource/x.p12")]
    [InlineData("https://evil.example.com/tenant-1-wallet/x.p12")]
    [InlineData("https://walverostorage.blob.core.windows.net/only-container")]
    [InlineData("https://walverostorage.blob.core.windows.net/")]
    [InlineData("ftp://walverostorage.blob.core.windows.net/c/x.p12")]
    public void NotOwnBlobUrl_Rejected(string? url)
        => WalletSecretPaths.TryParseOwnBlobUrl(url, out _, out _).Should().BeFalse();

    [Fact]
    public void Password_ProtectThenReveal_RoundTrips()
    {
        static string Enc(string s) => new string(s.Reverse().ToArray());
        static string? Dec(string s) => new string(s.Reverse().ToArray());

        var stored = WalletSecretPaths.Protect("p@ss", Enc);
        stored.Should().StartWith(WalletSecretPaths.EncryptedPrefix);
        stored.Should().NotContain("p@ss");
        WalletSecretPaths.IsEncrypted(stored).Should().BeTrue();
        WalletSecretPaths.Reveal(stored, Dec).Should().Be("p@ss");
    }

    [Fact]
    public void Password_LegacyPlaintext_ReturnedAsIs_And_NullOrEmpty()
    {
        WalletSecretPaths.Reveal("plain123", _ => throw new Exception("must not decrypt")).Should().Be("plain123");
        WalletSecretPaths.Reveal(null, _ => "x").Should().BeNull();
        WalletSecretPaths.Protect("", _ => "x").Should().BeEmpty();
        WalletSecretPaths.Protect(null, _ => "x").Should().BeEmpty();
    }

    [Fact]
    public void Password_DecryptFailure_ReturnsNull()
        => WalletSecretPaths.Reveal(WalletSecretPaths.EncryptedPrefix + "bad", _ => null).Should().BeNull();
}

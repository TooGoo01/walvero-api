using FluentAssertions;
using Loyalty.Business.Helpers;

namespace Loyalty.Tests.Helpers;

/// <summary>Telefonla bağlanan iiko qonağının köhnə balansı — idempotency açarı, gözləmə vəziyyəti, məbləğ normalizasiyası.</summary>
public class IikoLegacyBalanceTests
{
    private const string IikoId = "0F93173D-1111-2222-3333-444455556666";
    private const string WalletId = "AAAA0000-BBBB-CCCC-DDDD-EEEEFFFF0000";

    [Fact]
    public void OrderRef_IsCaseInsensitiveAndStable()
    {
        IikoLegacyBalance.OrderRef(IikoId, WalletId)
            .Should().Be("iiko-legacy:0f93173d-1111-2222-3333-444455556666:aaaa0000-bbbb-cccc-dddd-eeeeffff0000");
        IikoLegacyBalance.OrderRef(IikoId.ToLowerInvariant(), " " + WalletId.ToLowerInvariant() + " ")
            .Should().Be(IikoLegacyBalance.OrderRef(IikoId, WalletId));
    }

    [Fact]
    public void OrderRef_WithoutWallet_UsesDash()
    {
        IikoLegacyBalance.OrderRef(IikoId, null).Should().EndWith(":-");
    }

    [Theory]
    [InlineData(5.5, false, true)]   // balans var, import olunmayıb → gözləyir
    [InlineData(5.5, true, false)]   // import olunub
    [InlineData(0.0, false, false)]  // 0 balans → gözləmə yoxdur (push bloklanmır)
    [InlineData(null, false, false)] // telefonla bağlanmayıb
    public void IsPending(double? balance, bool imported, bool expected)
    {
        decimal? b = balance.HasValue ? (decimal)balance.Value : null;
        DateTime? at = imported ? DateTime.UtcNow : null;
        IikoLegacyBalance.IsPending(b, at).Should().Be(expected);
    }

    [Fact]
    public void Normalize_RoundsAndClampsNegative()
    {
        IikoLegacyBalance.Normalize(11.054m).Should().Be(11.05m);
        IikoLegacyBalance.Normalize(-3m).Should().Be(0m);
        IikoLegacyBalance.Normalize(null).Should().Be(0m);
    }
}

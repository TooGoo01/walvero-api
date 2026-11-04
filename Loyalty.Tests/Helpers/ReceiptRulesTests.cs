using FluentAssertions;
using Loyalty.Business.Services.Implementations.Main;
using Xunit;
using S = Loyalty.Business.Services.Implementations.Main.ReceiptValidationService;

namespace Loyalty.Tests.Helpers;

public class ReceiptRulesTests
{
    private const string BakuTz = "Azerbaijan Standard Time";

    // ---------- Tarix pəncərəsi (bu gün və ya dünən, tenant TZ-də) ----------

    // 2026-11-04 20:00 UTC = 2026-11-05 00:00 Bakı (UTC+4) → "bu gün" Bakıda Nov 5.
    private static readonly DateTime NowUtc = new(2026, 11, 4, 20, 0, 0, DateTimeKind.Utc);
    private static DateTime Noon(int d) => new(2026, 11, d, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Date_Today_InTenantZone_IsValid()
        => S.IsWithinReceiptWindow(Noon(5), BakuTz, S.MaxReceiptAgeDays, NowUtc).Should().BeTrue();

    [Fact]
    public void Date_Yesterday_IsValid()
        => S.IsWithinReceiptWindow(Noon(4), BakuTz, S.MaxReceiptAgeDays, NowUtc).Should().BeTrue();

    [Fact]
    public void Date_TwoDaysAgo_IsRejected()
        => S.IsWithinReceiptWindow(Noon(3), BakuTz, S.MaxReceiptAgeDays, NowUtc).Should().BeFalse();

    [Fact]
    public void Date_Tomorrow_IsRejected()
        => S.IsWithinReceiptWindow(Noon(6), BakuTz, S.MaxReceiptAgeDays, NowUtc).Should().BeFalse();

    [Fact]
    public void Date_UnknownTimeZone_FallsBackToUtc()
    {
        // UTC-də "bu gün" Nov 4 → Nov 4 valid, Nov 3 valid (dünən), Nov 2 rədd.
        S.IsWithinReceiptWindow(Noon(4), "No/Such_Zone", 1, NowUtc).Should().BeTrue();
        S.IsWithinReceiptWindow(Noon(3), "No/Such_Zone", 1, NowUtc).Should().BeTrue();
        S.IsWithinReceiptWindow(Noon(2), "No/Such_Zone", 1, NowUtc).Should().BeFalse();
    }

    // ---------- Sahib yoxlaması: VÖEN VƏ YA filial adı ----------

    [Fact]
    public void Ownership_NothingConfigured_IsNotConfigured()
        => S.EvaluateOwnership(null, null, "1701332401", "Papa Cinema", "x")
            .Should().Be(S.OwnershipVerdict.NotConfigured);

    [Fact]
    public void Ownership_TaxMatches_IsVerified_EvenWithoutBranchNames()
        => S.EvaluateOwnership("1701332401", null, "VÖEN: 1701 332 401", null, "")
            .Should().Be(S.OwnershipVerdict.Verified);

    [Fact]
    public void Ownership_TaxDiffers_IsTaxMismatch_EvenIfNameMatches()
        => S.EvaluateOwnership("1701332401", "Papa Cinema", "9999999999", "Papa Cinema", "PAPA CINEMA\nYekun 10.00")
            .Should().Be(S.OwnershipVerdict.TaxMismatch);

    [Fact]
    public void Ownership_NoTaxOnReceipt_ButBranchNameFound_IsVerified()
        => S.EvaluateOwnership("1701332401", "Papa Cinema,PapaCinema", null, null, "PAPA CİNEMA\nÖdəniş çeki\nSifariş № 12")
            .Should().Be(S.OwnershipVerdict.Verified);

    [Fact]
    public void Ownership_NameFound_InMerchantName_WithDiacritics_IsVerified()
        => S.EvaluateOwnership(null, "Şəhər Kafe", null, "SEHER KAFE MMC", "")
            .Should().Be(S.OwnershipVerdict.Verified);

    [Fact]
    public void Ownership_ConfiguredButNothingFound_IsNotVerified()
        => S.EvaluateOwnership("1701332401", "Papa Cinema", null, "Başqa Restoran", "BASQA RESTORAN\nYekun 20.00")
            .Should().Be(S.OwnershipVerdict.NotVerified);

    [Fact]
    public void Ownership_OnlyNamesConfigured_NotFound_IsNotVerified()
        => S.EvaluateOwnership(null, "Papa Cinema", "1701332401", null, "Some other text")
            .Should().Be(S.OwnershipVerdict.NotVerified);

    // ---------- Məbləğ azalması (xidmət haqqı) ----------

    [Theory]
    [InlineData(100, 10, 90)]
    [InlineData(45.55, 10, 41.00)]   // 40.995 → AwayFromZero → 41.00
    [InlineData(33.33, 0, 33.33)]
    [InlineData(50, 100, 0)]
    [InlineData(50, -5, 50)]
    public void NetAmount_AppliesPercent(decimal gross, decimal pct, decimal expected)
        => S.NetAmount(gross, pct).Should().Be(expected);

    [Fact]
    public void NormalizeTax_KeepsOnlyDigits_WhenAnyDigitPresent()
        => S.NormalizeTax(" VÖEN: 1701-332 401 ").Should().Be("1701332401");

    [Fact]
    public void NormalizeTax_NoDigits_FallsBackToAlnumUpper()
        => S.NormalizeTax(" ab-c ").Should().Be("ABC");

    [Fact]
    public void NormalizeTax_DigitsOnlyExpected_MatchesPrefixedFound()
        => S.NormalizeTax("1701332401").Should().Be(S.NormalizeTax("1701 332 401"));
}

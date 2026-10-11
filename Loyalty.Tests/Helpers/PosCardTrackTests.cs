using FluentAssertions;
using Loyalty.Business.Helpers;
using Loyalty.DataAccess.Enums;

namespace Loyalty.Tests.Helpers;

/// <summary>iiko kart track rejimi — əsas track seçimi, bütün track-lərin sırası, qısa kod tanınması.</summary>
public class PosCardTrackTests
{
    private const string Barcode = "789567890655";
    private const string WalveroId = "994100123";

    [Fact]
    public void FullBarcode_PrimaryIsBarcode()
    {
        PosCardTrack.Primary(PosCardTrackMode.FullBarcode, null, Barcode, WalveroId).Should().Be(Barcode);
        PosCardTrack.AllTracks(PosCardTrackMode.FullBarcode, null, Barcode, WalveroId)
            .Should().Equal(Barcode, WalveroId);
    }

    [Fact]
    public void WalveroId_PrimaryIsWalveroId_FallsBackToBarcodeWithoutApp()
    {
        PosCardTrack.Primary(PosCardTrackMode.WalveroId, null, Barcode, WalveroId).Should().Be(WalveroId);
        PosCardTrack.AllTracks(PosCardTrackMode.WalveroId, null, Barcode, WalveroId)
            .Should().Equal(WalveroId, Barcode);

        // app-sız müştəri: WalveroId yoxdur → tam barkod
        PosCardTrack.Primary(PosCardTrackMode.WalveroId, null, Barcode, null).Should().Be(Barcode);
        PosCardTrack.AllTracks(PosCardTrackMode.WalveroId, null, Barcode, null).Should().Equal(Barcode);
    }

    [Theory]
    [InlineData(4, "0655")]
    [InlineData(5, "90655")]
    [InlineData(6, "890655")]
    [InlineData(8, "67890655")]
    public void LastDigits_PrimaryIsSuffix_FullBarcodeStillAdded(int n, string expected)
    {
        PosCardTrack.Primary(PosCardTrackMode.LastDigits, n, Barcode, WalveroId).Should().Be(expected);
        PosCardTrack.AllTracks(PosCardTrackMode.LastDigits, n, Barcode, WalveroId)
            .Should().Equal(expected, Barcode, WalveroId);
    }

    [Fact]
    public void LastDigits_DigitsOutOfRange_Clamped_NullDefaultsToSix()
    {
        PosCardTrack.Primary(PosCardTrackMode.LastDigits, null, Barcode, null).Should().Be("890655");
        PosCardTrack.Primary(PosCardTrackMode.LastDigits, 2, Barcode, null).Should().Be("0655");
        PosCardTrack.Primary(PosCardTrackMode.LastDigits, 20, Barcode, null).Should().Be("67890655");
    }

    [Fact]
    public void LastDigits_ShortValue_ReturnsItself_NoDuplicateTracks()
    {
        PosCardTrack.LastDigits("1234", 6).Should().Be("1234");
        // barkod 4 rəqəmdirsə qısa kod = barkod → siyahıda bir dəfə
        PosCardTrack.AllTracks(PosCardTrackMode.LastDigits, 6, "1234", null).Should().Equal("1234");
    }

    [Fact]
    public void Empty_Inputs_Handled()
    {
        PosCardTrack.Primary(PosCardTrackMode.FullBarcode, null, null, null).Should().BeNull();
        PosCardTrack.Primary(PosCardTrackMode.LastDigits, 6, "  ", null).Should().BeNull();
        PosCardTrack.AllTracks(PosCardTrackMode.LastDigits, 6, null, " ").Should().BeEmpty();
    }

    [Theory]
    [InlineData("890655", 6, true)]
    [InlineData("0655", 4, true)]
    [InlineData("890655", 4, false)]   // uzunluq ≠ N
    [InlineData("89O655", 6, false)]   // hərf
    [InlineData("789567890655", 6, false)]
    [InlineData("", 6, false)]
    [InlineData(null, 6, false)]
    public void IsShortCode(string? code, int digits, bool expected)
        => PosCardTrack.IsShortCode(code, digits).Should().Be(expected);

    [Fact]
    public void Describe_Labels()
    {
        PosCardTrack.Describe(PosCardTrackMode.FullBarcode, null).Should().Be("Tam barkod");
        PosCardTrack.Describe(PosCardTrackMode.WalveroId, null).Should().Be("Walvero ID");
        PosCardTrack.Describe(PosCardTrackMode.LastDigits, 5).Should().Be("Son 5 rəqəm");
        PosCardTrack.Describe(PosCardTrackMode.LastDigits, null).Should().Be("Son 6 rəqəm");
    }
}

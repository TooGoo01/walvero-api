using FluentAssertions;
using Loyalty.Business.Helpers;

namespace Loyalty.Tests.Helpers;

/// <summary>iiko OrderClosed: sifarişdə verilmiş hədiyyələrin tanınması (endirim növü + kassa kodları + köhnə yol).</summary>
public class GiftRedeemMatcherTests
{
    private const string DessertDiscount = "4581b40b-2cdc-1642-019b-e5c3a66aebce";
    private const string DrinkDiscount = "3654748d-d44f-c57b-0194-238e01e4aa2a";
    private const string WalveroDiscount = "aaaa0000-0000-0000-0000-000000000001";
    private static readonly HashSet<string> NoLegacy = new(StringComparer.OrdinalIgnoreCase);

    private static readonly GiftRewardInfo Dessert = new(10, "Pulsuz 1 dessert", 7, DessertDiscount, new[] { "01755", "01753", "02004" });
    private static readonly GiftRewardInfo Drink = new(11, "Bir pulsuz içki", 10, DrinkDiscount, new[] { "01707" });

    private static GiftOrderLine Line(string guid, string? code, decimal amount = 1) => new(guid, code, amount);
    private static GiftOrderDiscount Disc(string type, params string?[] items) => new(type, items);

    [Fact]
    public void DiscountType_WithItems_CountsLines()
    {
        var r = GiftRedeemMatcher.Match(
            new[] { Line("g-cake", "01755"), Line("g-latte", "00023", 2) },
            new[] { Disc(DessertDiscount, "g-cake") },
            NoLegacy, new[] { Dessert, Drink });

        r.Rewards.Should().ContainSingle().Which.Should().Be((Dessert, 1));
        r.RedeemedUnitsByProduct.Should().ContainKey("01755").WhoseValue.Should().Be(1);
        r.TotalPointsCost.Should().Be(7);
        r.UnmappedProductKeys.Should().BeEmpty();
    }

    [Fact]
    public void DiscountType_SizedWildcardProduct_StillRecognised()
    {
        // İçki "*" ölçülü hədiyyəsi — məhsul kodu hədiyyə siyahısında yoxdur, endirim növü kifayətdir
        var r = GiftRedeemMatcher.Match(
            new[] { Line("g-cap", "00031"), Line("g-cap2", "00032") },
            new[] { Disc(DrinkDiscount, "g-cap", "g-cap2") },
            NoLegacy, new[] { Dessert, Drink });

        r.Rewards.Should().ContainSingle().Which.Should().Be((Drink, 2));
        r.RedeemedUnitsByProduct.Keys.Should().BeEquivalentTo("00031", "00032");
    }

    [Fact]
    public void DiscountType_WithoutItems_CountsGiftProductsInOrder()
    {
        var r = GiftRedeemMatcher.Match(
            new[] { Line("g-cake", "01753", 2), Line("g-latte", "00023") },
            new[] { Disc(DessertDiscount) },
            NoLegacy, new[] { Dessert });

        r.Rewards.Should().ContainSingle().Which.Should().Be((Dessert, 2));
        r.RedeemedUnitsByProduct["01753"].Should().Be(2);
    }

    [Fact]
    public void DiscountType_WithoutItems_NoGiftProduct_CountsOne()
    {
        var r = GiftRedeemMatcher.Match(
            new[] { Line("g-cap", "00031") },
            new[] { Disc(DrinkDiscount) },
            NoLegacy, new[] { Drink });

        r.Rewards.Should().ContainSingle().Which.Should().Be((Drink, 1));
        r.RedeemedUnitsByProduct.Should().BeEmpty();
    }

    [Fact]
    public void UnrelatedDiscount_Ignored()
    {
        var r = GiftRedeemMatcher.Match(
            new[] { Line("g-cake", "01755") },
            new[] { Disc("9ecd1fd8-4166-4879-aba4-b4802cc2c7c0", "g-cake") },
            NoLegacy, new[] { Dessert });

        r.Rewards.Should().BeEmpty();
        r.RedeemedUnitsByProduct.Should().BeEmpty();
    }

    [Fact]
    public void LegacyWalveroDiscount_MatchesByProductCode()
    {
        var doner = new GiftRewardInfo(20, "Donər", 50, null, new[] { "D-01" });
        var legacy = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { WalveroDiscount };

        var r = GiftRedeemMatcher.Match(
            new[] { Line("g-doner", "D-01"), Line("g-cola", "C-01") },
            new[] { Disc(WalveroDiscount, "g-doner", "g-cola") },
            legacy, new[] { doner });

        r.Rewards.Should().ContainSingle().Which.Should().Be((doner, 1));
        r.UnmappedProductKeys.Should().Equal("C-01");
        r.RedeemedUnitsByProduct.Keys.Should().BeEquivalentTo("D-01", "C-01"); // köhnə davranış: endirimli sətirlər qazancdan düşür
    }

    [Fact]
    public void LegacyWalveroDiscount_WithoutItems_FallsBackToOrderProducts()
    {
        var doner = new GiftRewardInfo(20, "Donər", 50, null, new[] { "D-01" });
        var legacy = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { WalveroDiscount };

        var r = GiftRedeemMatcher.Match(
            new[] { Line("g-doner", "D-01", 2) },
            new[] { Disc(WalveroDiscount) },
            legacy, new[] { doner });

        r.Rewards.Should().ContainSingle().Which.Should().Be((doner, 2));
        r.TotalPointsCost.Should().Be(100);
    }

    [Fact]
    public void LegacyReward_KeyedByGuid_StillMatches()
    {
        var byGuid = new GiftRewardInfo(21, "Köhnə", 5, null, new[] { "g-old" });
        var legacy = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { WalveroDiscount };

        var r = GiftRedeemMatcher.Match(
            new[] { Line("g-old", null) },
            new[] { Disc(WalveroDiscount, "g-old") },
            legacy, new[] { byGuid });

        r.Rewards.Should().ContainSingle().Which.Should().Be((byGuid, 1));
    }

    [Fact]
    public void TwoGiftsInOneOrder_KeptSeparately()
    {
        var r = GiftRedeemMatcher.Match(
            new[] { Line("g-cake", "01755"), Line("g-cap", "00031") },
            new[] { Disc(DessertDiscount, "g-cake"), Disc(DrinkDiscount, "g-cap") },
            NoLegacy, new[] { Dessert, Drink });

        r.Rewards.Select(x => (x.Reward.Id, x.Count)).Should().Equal((10, 1), (11, 1));
        r.TotalUnits.Should().Be(2);
        r.TotalPointsCost.Should().Be(17);
    }
}

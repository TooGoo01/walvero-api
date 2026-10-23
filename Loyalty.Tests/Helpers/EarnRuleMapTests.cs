using FluentAssertions;
using Loyalty.Business.Helpers;
using Loyalty.DataAccess.Entities.Main;

namespace Loyalty.Tests.Helpers;

/// <summary>Məhsul / qrup / kateqoriya qazanc qaydaları (PosProductEarnRule.Kind) — sifariş sətri lookup-u.</summary>
public class EarnRuleMapTests
{
    private const string CatDessert = "c0000000-0000-0000-0000-000000000001";
    private const string CatHotBar = "c0000000-0000-0000-0000-000000000002";
    private const string GrpCakes = "a0000000-0000-0000-0000-000000000001";
    private const string GrpCakesSub = "a0000000-0000-0000-0000-000000000002";
    private const string Cheesecake = "b0000000-0000-0000-0000-000000000001";
    private const string Tiramisu = "b0000000-0000-0000-0000-000000000002";
    private const string Latte = "b0000000-0000-0000-0000-000000000003";

    private static readonly NomenclatureSnapshot Nom = new()
    {
        Groups = new()
        {
            new NomenclatureGroup(GrpCakes, "Tortlar", null),
            new NomenclatureGroup(GrpCakesSub, "Çizkeyk", GrpCakes),
        },
        Products = new()
        {
            new NomenclatureProduct(Cheesecake, "02004", "Cheesecake", GrpCakesSub, CatDessert, "Dish"),
            new NomenclatureProduct(Tiramisu, "02006", "Tiramisu", GrpCakes, CatDessert, "Dish"),
            new NomenclatureProduct(Latte, "00023", "Latte", null, CatHotBar, "Dish"),
        }
    };

    private static PosProductEarnRule R(int id, PosEarnExclusionKind kind, string externalId, int points,
        int? programId = null, string? code = null)
        => new()
        {
            Id = id, Kind = kind, ExternalProductId = externalId, Code = code, PointsPerUnit = points,
            LoyaltyProgramId = programId
        };

    private static EarnLine Line(string? productId, string? number, string? categoryId, decimal amount = 1)
        => new(productId, number, categoryId, null, amount, 5m * amount);

    [Fact]
    public void Category_MatchesByPluginCategoryId_WithoutNomenclature()
    {
        var map = EarnRuleMap.Build(new[] { R(1, PosEarnExclusionKind.Category, CatDessert, 1) }, 10, null);

        map.Lookup(Line("x-unknown", "09999", CatDessert))!.PointsPerUnit.Should().Be(1);
        map.Lookup(Line(Latte, "00023", CatHotBar)).Should().BeNull();
        map.Lookup(Line(Latte, "00023", null)).Should().BeNull();
    }

    [Fact]
    public void Category_ResolvesThroughNomenclature_WhenPluginSendsNoCategory()
    {
        var map = EarnRuleMap.Build(new[] { R(1, PosEarnExclusionKind.Category, CatDessert, 2) }, 10, Nom);

        map.Lookup(Line(Tiramisu, null, null))!.PointsPerUnit.Should().Be(2);
        map.Lookup(Line(null, "02004", null))!.PointsPerUnit.Should().Be(2);
    }

    [Fact]
    public void ProductBeatsGroupBeatsCategory()
    {
        var map = EarnRuleMap.Build(new[]
        {
            R(1, PosEarnExclusionKind.Category, CatDessert, 1),
            R(2, PosEarnExclusionKind.Group, GrpCakes, 3),
            R(3, PosEarnExclusionKind.Product, "02004", 5),   // kod ilə məhsul qaydası
        }, 10, Nom);

        map.Lookup(Line(Cheesecake, "02004", CatDessert))!.PointsPerUnit.Should().Be(5);
        map.Lookup(Line(Tiramisu, "02006", CatDessert))!.PointsPerUnit.Should().Be(3);
        // Nomenklaturada olmayan desert — yalnız plugin kateqoriyası ilə
        map.Lookup(Line("new-dessert", "02999", CatDessert))!.PointsPerUnit.Should().Be(1);
    }

    [Fact]
    public void Group_IncludesSubGroups()
    {
        var map = EarnRuleMap.Build(new[] { R(1, PosEarnExclusionKind.Group, GrpCakes, 4) }, 10, Nom);

        map.Lookup(Line(Cheesecake, null, null))!.PointsPerUnit.Should().Be(4);
        map.Lookup(Line(Latte, null, CatHotBar)).Should().BeNull();
    }

    [Fact]
    public void ProgramSpecificRuleBeatsGeneral_OtherProgramIgnored()
    {
        var rules = new[]
        {
            R(1, PosEarnExclusionKind.Category, CatDessert, 1),
            R(2, PosEarnExclusionKind.Category, CatDessert, 7, programId: 20),
            R(3, PosEarnExclusionKind.Category, CatHotBar, 9, programId: 99),
        };

        EarnRuleMap.Build(rules, 20, null).Lookup(Line(null, "1", CatDessert))!.PointsPerUnit.Should().Be(7);
        EarnRuleMap.Build(rules, 10, null).Lookup(Line(null, "1", CatDessert))!.PointsPerUnit.Should().Be(1);
        EarnRuleMap.Build(rules, 10, null).Lookup(Line(null, "1", CatHotBar)).Should().BeNull();
    }

    [Fact]
    public void LegacyProductRule_StillMatchesByGuidOrCode()
    {
        var map = EarnRuleMap.Build(new[]
        {
            R(1, PosEarnExclusionKind.Product, Latte, 1),
            R(2, PosEarnExclusionKind.Product, "02006", 2),
        }, 10, null);

        map.Lookup(Line(Latte, "00023", CatHotBar))!.PointsPerUnit.Should().Be(1);
        map.Lookup(Line("other-guid", "02006", CatDessert))!.PointsPerUnit.Should().Be(2);
    }

    [Fact]
    public void ApplyEarnRules_CategoryRule_SkipsRedeemedGiftLine()
    {
        var map = EarnRuleMap.Build(new[] { R(1, PosEarnExclusionKind.Category, CatDessert, 1) }, 10, null);
        var lines = new[]
        {
            Line(Cheesecake, "02004", CatDessert, amount: 2),
            Line(Tiramisu, "02006", CatDessert),    // hədiyyə kimi verilib
            Line(Latte, "00023", CatHotBar),
        };

        var r = EarnExclusionMath.ApplyEarnRules(lines, map.Lookup, l => l.ProductNumber == "02006", EarnExclusionMap.Empty);

        r.Points.Should().Be(2);
        r.AnyRuleMatched.Should().BeTrue();
    }
}

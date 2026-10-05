using FluentAssertions;
using Loyalty.Business.Helpers;
using Loyalty.DataAccess.Entities.Main;

namespace Loyalty.Tests.Helpers;

/// <summary>Bonus istisnaları — qayda prioriteti (məhsul → dərin qrup → kateqoriya) və bonus bazası hesabı.</summary>
public class EarnExclusionMathTests
{
    // Bar (kök) → Alkoqol (alt) → Pivə (alt-alt); Mətbəx (kök)
    private const string Bar = "a0000000-0000-0000-0000-000000000001";
    private const string Alcohol = "a0000000-0000-0000-0000-000000000002";
    private const string BeerGroup = "a0000000-0000-0000-0000-000000000003";
    private const string Kitchen = "a0000000-0000-0000-0000-000000000004";
    private const string CatDrinks = "c0000000-0000-0000-0000-000000000001";
    private const string CatTobacco = "c0000000-0000-0000-0000-000000000002";

    private const string Beer = "b0000000-0000-0000-0000-000000000001";
    private const string Wine = "b0000000-0000-0000-0000-000000000002";
    private const string Juice = "b0000000-0000-0000-0000-000000000003";
    private const string Steak = "b0000000-0000-0000-0000-000000000004";
    private const string Cigarettes = "b0000000-0000-0000-0000-000000000005";

    private static readonly NomenclatureSnapshot Nom = new()
    {
        Groups = new()
        {
            new(Bar, "Bar", null),
            new(Alcohol, "Alkoqol", Bar),
            new(BeerGroup, "Pivə", Alcohol),
            new(Kitchen, "Mətbəx", null)
        },
        Categories = new() { new(CatDrinks, "İçkilər"), new(CatTobacco, "Tütün") },
        Products = new()
        {
            new(Beer, "101", "Efes", BeerGroup, CatDrinks, "Good"),
            new(Wine, "102", "Şərab", Alcohol, CatDrinks, "Good"),
            new(Juice, "103", "Şirə", Bar, CatDrinks, "Good"),
            new(Steak, "201", "Steyk", Kitchen, null, "Dish"),
            new(Cigarettes, "301", "Siqaret", null, CatTobacco, "Good")
        }
    };

    private static EarnExclusionRule Rule(PosEarnExclusionKind kind, string id, bool cashback = true, bool stamps = true, string? code = null)
        => new(kind, id, code, id, cashback, stamps);

    private static EarnExclusionMap Map(params EarnExclusionRule[] rules) => EarnExclusionMap.Build(rules, Nom);

    // ───────── Qayda prioriteti ─────────

    [Fact]
    public void NoRules_IsEmpty() => Map().IsEmpty.Should().BeTrue();

    [Fact]
    public void Group_CoversNestedSubgroups()
    {
        var map = Map(Rule(PosEarnExclusionKind.Group, Alcohol, cashback: true, stamps: false));
        map.Blocked(Beer, null, null)!.BlockCashback.Should().BeTrue();   // Pivə ⊂ Alkoqol
        map.Blocked(Beer, null, null)!.BlockStamps.Should().BeFalse();
        map.Blocked(Wine, null, null).Should().NotBeNull();
        map.Blocked(Juice, null, null).Should().BeNull();                  // Bar-dadır, Alkoqolda yox
        map.Blocked(Steak, null, null).Should().BeNull();
    }

    [Fact]
    public void DeeperGroup_OverridesParent()
    {
        var map = Map(
            Rule(PosEarnExclusionKind.Group, Bar),                                  // hamısı bloklu
            Rule(PosEarnExclusionKind.Group, BeerGroup, cashback: false, stamps: false)); // pivə istisna
        map.Blocked(Beer, null, null).Should().BeNull();
        map.Lookup(Beer, null, null)!.Any.Should().BeFalse();
        map.Blocked(Wine, null, null).Should().NotBeNull();
    }

    [Fact]
    public void ProductRule_OverridesGroup()
    {
        var map = Map(
            Rule(PosEarnExclusionKind.Group, Alcohol),
            Rule(PosEarnExclusionKind.Product, Beer, cashback: false, stamps: true));
        var b = map.Blocked(Beer, null, null)!;
        b.BlockCashback.Should().BeFalse();
        b.BlockStamps.Should().BeTrue();
        b.SourceKind.Should().Be(PosEarnExclusionKind.Product);
    }

    [Fact]
    public void ProductRule_BothFalse_IsWhitelist()
    {
        var map = Map(
            Rule(PosEarnExclusionKind.Group, Alcohol),
            Rule(PosEarnExclusionKind.Product, Wine, cashback: false, stamps: false));
        map.Blocked(Wine, null, null).Should().BeNull();
        map.Blocked(Wine, "102", CatDrinks).Should().BeNull(); // kod / kateqoriya fallback-ı da açılmır
        map.Blocked(Beer, null, null).Should().NotBeNull();
    }

    [Fact]
    public void Group_BeatsCategory()
    {
        var map = Map(
            Rule(PosEarnExclusionKind.Group, Alcohol, cashback: true, stamps: false),
            Rule(PosEarnExclusionKind.Category, CatDrinks, cashback: false, stamps: true));
        map.Blocked(Wine, null, null)!.SourceKind.Should().Be(PosEarnExclusionKind.Group);
        map.Blocked(Wine, null, null)!.BlockStamps.Should().BeFalse();
        // Şirə Alkoqolda deyil → kateqoriya qaydası
        var juice = map.Blocked(Juice, null, null)!;
        juice.SourceKind.Should().Be(PosEarnExclusionKind.Category);
        juice.BlockCashback.Should().BeFalse();
        juice.BlockStamps.Should().BeTrue();
    }

    [Fact]
    public void Category_FromNomenclature()
    {
        var map = Map(Rule(PosEarnExclusionKind.Category, CatTobacco));
        map.Blocked(Cigarettes, null, null).Should().NotBeNull();
        map.Blocked(Steak, null, null).Should().BeNull();
    }

    [Fact]
    public void CodeFallback_WhenProductIdUnknown()
    {
        var map = Map(Rule(PosEarnExclusionKind.Group, Alcohol));
        map.Blocked("ffffffff-0000-0000-0000-000000000000", "101", null).Should().NotBeNull();
    }

    [Fact]
    public void PluginCategoryFallback_WithoutNomenclature()
    {
        var map = EarnExclusionMap.Build(new[] { Rule(PosEarnExclusionKind.Category, CatTobacco) }, null);
        map.Blocked("unknown", null, CatTobacco).Should().NotBeNull();
        map.Blocked("unknown", null, CatDrinks).Should().BeNull();
    }

    [Fact]
    public void ManualNonGuidProductId_MatchesProductNumber()
    {
        var map = EarnExclusionMap.Build(new[] { Rule(PosEarnExclusionKind.Product, "5501") }, null);
        map.Blocked("d0000000-0000-0000-0000-000000000009", "5501", null).Should().NotBeNull();
    }

    [Fact]
    public void GroupCycle_DoesNotLoop()
    {
        var nom = new NomenclatureSnapshot
        {
            Groups = new() { new("g1", "G1", "g2"), new("g2", "G2", "g1") },
            Products = new() { new("p1", null, "P1", "g1", null, "Dish") }
        };
        var map = EarnExclusionMap.Build(new[] { Rule(PosEarnExclusionKind.Group, "g9") }, nom);
        map.Blocked("p1", null, null).Should().BeNull();
    }

    // ───────── Bonus bazası ─────────

    private static readonly EarnExclusionMap AlcoholBlocked = Map(Rule(PosEarnExclusionKind.Group, Alcohol));
    private static EarnLine Line(string id, decimal sum, decimal amount = 1) => new(id, null, null, id, amount, sum);
    private static readonly EarnItemDiscount[] NoDisc = Array.Empty<EarnItemDiscount>();

    [Fact]
    public void NoExclusions_LegacyBase()
    {
        var items = new[] { Line(Steak, 100) };
        EarnExclusionMath.Compute(items, NoDisc, 100, 20, false, EarnExclusionMap.Empty).CashbackBase.Should().Be(80);
        EarnExclusionMath.Compute(items, NoDisc, 100, 20, true, EarnExclusionMap.Empty).CashbackBase.Should().Be(100);
        EarnExclusionMath.Compute(items, NoDisc, 100, 20, false, AlcoholBlocked).CashbackBase.Should().Be(80); // blok yoxdur
    }

    [Fact]
    public void BlockedLine_SubtractedFromBase()
    {
        var r = EarnExclusionMath.Compute(new[] { Line(Beer, 30), Line(Steak, 70) }, NoDisc, 100, 0, false, AlcoholBlocked);
        r.CashbackBase.Should().Be(70);
        r.ExcludedNet.Should().Be(30);
        r.BlockedLines.Should().ContainSingle();
    }

    [Fact]
    public void PerItemDiscount_ReducesExcluded()
    {
        var r = EarnExclusionMath.Compute(
            new[] { Line(Beer, 30), Line(Steak, 70) },
            new[] { new EarnItemDiscount(Beer, 10) }, 90, 0, false, AlcoholBlocked);
        r.ExcludedNet.Should().Be(20);
        r.CashbackBase.Should().Be(70);
    }

    [Fact]
    public void OrderLevelDiscount_Proportional()
    {
        // FullSum 100, TotalSum 80 (20% sifariş endirimi, DiscountByItem yoxdur)
        var r = EarnExclusionMath.Compute(new[] { Line(Beer, 30), Line(Steak, 70) }, NoDisc, 80, 0, false, AlcoholBlocked);
        r.ExcludedNet.Should().Be(24);
        r.CashbackBase.Should().Be(56);
    }

    [Fact]
    public void WalletPayment_SplitProportionally()
    {
        var r = EarnExclusionMath.Compute(new[] { Line(Beer, 30), Line(Steak, 70) }, NoDisc, 100, 50, false, AlcoholBlocked);
        r.CashbackBase.Should().Be(35); // 70 − 50 × 70/100
    }

    [Fact]
    public void AllBlocked_ZeroBase()
    {
        var r = EarnExclusionMath.Compute(new[] { Line(Beer, 30), Line(Wine, 20) }, NoDisc, 50, 0, false, AlcoholBlocked);
        r.CashbackBase.Should().Be(0);
        r.ExcludedNet.Should().Be(50);
    }

    [Fact]
    public void ProgressCard_IgnoresWallet()
    {
        var r = EarnExclusionMath.Compute(new[] { Line(Beer, 30), Line(Steak, 70) }, NoDisc, 100, 40, true, AlcoholBlocked);
        r.CashbackBase.Should().Be(70);
    }

    [Fact]
    public void StampsOnlyBlock_DoesNotTouchBase()
    {
        var map = Map(Rule(PosEarnExclusionKind.Group, Alcohol, cashback: false, stamps: true));
        var r = EarnExclusionMath.Compute(new[] { Line(Beer, 30), Line(Steak, 70) }, NoDisc, 100, 0, false, map);
        r.CashbackBase.Should().Be(100);
        r.ExcludedNet.Should().Be(0);
        r.AnyBlocked.Should().BeTrue();
    }

    [Fact]
    public void SameProductTwoLines_DiscountSharedByProduct()
    {
        var r = EarnExclusionMath.Compute(
            new[] { Line(Beer, 20), Line(Beer, 10), Line(Steak, 70) },
            new[] { new EarnItemDiscount(Beer, 6) }, 94, 0, false, AlcoholBlocked);
        r.ExcludedNet.Should().Be(24);
        r.CashbackBase.Should().Be(70);
        r.BlockedLines.Select(l => l.Net).Should().Equal(16m, 8m);
    }

    // ───────── Məhsul bonusu (earn rule) ─────────

    private static EarnRuleHit? OneStep(EarnLine l) => l.ProductId is Beer or Steak ? new EarnRuleHit(1, 0) : null;

    [Fact]
    public void EarnRules_BlockedStampsSkipped_MatchKept()
    {
        var r = EarnExclusionMath.ApplyEarnRules(
            new[] { Line(Steak, 70, 2), Line(Beer, 30, 3), Line(Juice, 5) }, OneStep, _ => false, AlcoholBlocked);
        r.Steps.Should().Be(2);
        r.SkippedUnits.Should().Be(3);
        r.AnyRuleMatched.Should().BeTrue();
    }

    [Fact]
    public void EarnRules_OnlyBlockedMatched_StillMatched()
    {
        // Rejim dəyişmir: bloklanmış möhür məhsulu cashback fallback-ını açmamalıdır
        var r = EarnExclusionMath.ApplyEarnRules(new[] { Line(Beer, 30) }, OneStep, _ => false, AlcoholBlocked);
        r.AnyRuleMatched.Should().BeTrue();
        r.Steps.Should().Be(0);
    }

    [Fact]
    public void EarnRules_CashbackOnlyBlock_StillGivesStamps()
    {
        var map = Map(Rule(PosEarnExclusionKind.Group, Alcohol, cashback: true, stamps: false));
        var r = EarnExclusionMath.ApplyEarnRules(new[] { Line(Beer, 30, 2) }, OneStep, _ => false, map);
        r.Steps.Should().Be(2);
        r.SkippedUnits.Should().Be(0);
    }

    [Fact]
    public void EarnRules_RedeemedItem_NotMatched()
    {
        var r = EarnExclusionMath.ApplyEarnRules(new[] { Line(Steak, 70) }, OneStep, _ => true, EarnExclusionMap.Empty);
        r.AnyRuleMatched.Should().BeFalse();
        r.Steps.Should().Be(0);
    }

    [Fact]
    public void DescribeSuffix_Format()
    {
        var r = EarnExclusionMath.Compute(new[] { Line(Beer, 12.5m), Line(Steak, 70) }, NoDisc, 82.5m, 0, false, AlcoholBlocked);
        EarnExclusionMath.DescribeSuffix(r, 2).Should().Be(" · bonussuz məhsullar −12.5 ₼ · möhürsüz 2 əd.");
        EarnExclusionMath.DescribeSuffix(new EarnBaseResult { CashbackBase = 10 }, 0).Should().BeEmpty();
    }
}

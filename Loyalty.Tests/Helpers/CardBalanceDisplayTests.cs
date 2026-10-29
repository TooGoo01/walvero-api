using FluentAssertions;
using Loyalty.Business.Dtos.Post.Main;
using Loyalty.Business.Helpers;
using Loyalty.DataAccess.Enums;

namespace Loyalty.Tests.Helpers;

/// <summary>Kartda endirim faizi, balans formatı (tam ədəd), balans vahidi override, dəyişmə bildirişi mətni.</summary>
public class CardBalanceDisplayTests
{
    private static CardTemplateSnapshot Points(string currency, CardDesignSettings? d = null) => new()
    {
        TemplateType = CardTemplateType.PointsBased, Currency = currency, BronzePercent = 5, Design = d ?? new CardDesignSettings()
    };

    private static Dictionary<string, CardFieldValue> Values(CardTemplateSnapshot s, decimal points, decimal? discount = null)
        => CardFieldCatalog.BuildValues(s, "Ad Soyad", "123", points, 0, 0, 0, null, CardPlatform.Apple, discount);

    [Fact]
    public void Units_AutoFormat_ShowsInteger()
    {
        Values(Points("məhsul"), 10.00m)[CardFieldCatalog.Balance].Value.Should().Be("10 məhsul");
        Values(Points("məhsul"), 10.5m)[CardFieldCatalog.Balance].Value.Should().Be("10.5 məhsul");   // dəyər gizlədilmir
    }

    [Fact]
    public void Money_AutoFormat_Unchanged()
    {
        Values(Points("AZN"), 10.00m)[CardFieldCatalog.Balance].Value.Should().Be($"{10.00m} AZN");
        Values(Points("₼"), 12.50m)[CardFieldCatalog.Balance].Value.Should().Be($"{12.50m} ₼");
    }

    [Fact]
    public void ExplicitFormat_OverridesAuto()
    {
        Values(Points("AZN", new CardDesignSettings { BalanceFormat = BalanceFormat.Integer }), 10.00m)[CardFieldCatalog.Balance].Value.Should().Be("10 AZN");
        Values(Points("məhsul", new CardDesignSettings { BalanceFormat = BalanceFormat.Decimal }), 10.00m)[CardFieldCatalog.Balance].Value.Should().Be($"{10.00m} məhsul");
    }

    [Fact]
    public void BalanceUnit_OverridesCurrencyWord_FormatStillByTemplateCurrency()
    {
        var s = Points("məhsul", new CardDesignSettings { BalanceUnit = " ədəd " });
        CardFieldCatalog.EffectiveUnit(s).Should().Be("ədəd");
        Values(s, 7.00m)[CardFieldCatalog.Balance].Value.Should().Be("7 ədəd");
        CardFieldCatalog.EffectiveUnit(Points("")).Should().Be("AZN");
    }

    [Fact]
    public void Discount_EmptyWhenNone_PercentWhenSet()
    {
        Values(Points("AZN"), 1m)[CardFieldCatalog.Discount].Value.Should().BeEmpty();
        var v = Values(Points("AZN"), 1m, 20.00m)[CardFieldCatalog.Discount];
        v.Value.Should().Be("20%");
        v.DefaultLabel.Should().Be("Endirim");
        Values(Points("AZN"), 1m, 12.5m)[CardFieldCatalog.Discount].Value.Should().Be("12.5%");
    }

    [Fact]
    public void Discount_StaysVisible_InLayoutSavedBeforeFieldExisted()
    {
        var saved = new List<CardFieldSetting> { new() { Id = CardFieldCatalog.Balance, Slot = CardFieldSlot.Header, Order = 1 } };
        var eff = CardFieldCatalog.Effective(saved, CardTemplateType.PointsBased, out _).ToDictionary(f => f.Id);
        eff[CardFieldCatalog.Discount].Slot.Should().Be(CardFieldSlot.Auxiliary);
        eff[CardFieldCatalog.Cashback].Slot.Should().Be(CardFieldSlot.Hidden);   // köhnə qayda: siyahıda yoxdur → gizli

        saved.Add(new() { Id = CardFieldCatalog.Discount, Slot = CardFieldSlot.Hidden });
        CardFieldCatalog.Effective(saved, CardTemplateType.PointsBased, out _)
            .Single(f => f.Id == CardFieldCatalog.Discount).Slot.Should().Be(CardFieldSlot.Hidden);
    }

    [Fact]
    public void Discount_GoogleRows_NextToCashback()
    {
        var eff = CardFieldCatalog.Effective(null, CardTemplateType.PointsBased, out _);
        CardFieldCatalog.GoogleRows(eff, GoogleBannerPosition.Top, out _).Last().Should().Equal(CardFieldCatalog.Cashback, CardFieldCatalog.Discount);

        var saved = new List<CardFieldSetting>
        {
            new() { Id = CardFieldCatalog.Cashback, Slot = CardFieldSlot.Auxiliary, Order = 1 },
            new() { Id = CardFieldCatalog.Discount, Slot = CardFieldSlot.Auxiliary, Order = 2 },
        };
        var rows = CardFieldCatalog.GoogleRows(CardFieldCatalog.Effective(saved, CardTemplateType.PointsBased, out _), GoogleBannerPosition.None, out _);
        rows.Should().ContainSingle().Which.Should().Equal(CardFieldCatalog.Cashback, CardFieldCatalog.Discount);
    }

    [Fact]
    public void Discount_Progress_DefaultSecondary()
        => CardFieldCatalog.DefaultFields(CardTemplateType.ProgressBased)
            .Single(f => f.Id == CardFieldCatalog.Discount).Slot.Should().Be(CardFieldSlot.Secondary);

    [Theory]
    [InlineData("cat", "ENDİRİM 20%", null, 20)]
    [InlineData("cat", "Endirim 30 %", null, 30)]
    [InlineData("cat", "VIP 7,5%", null, 7.5)]
    [InlineData("cat", "ENDİRİM 20%", 25.0, 25)]   // saxlanmış faiz üstündür
    public void PosDiscount_Percent(string id, string name, double? stored, double expected)
        => PosDiscount.Percent(id, name, (decimal?)stored).Should().Be((decimal)expected);

    [Theory]
    [InlineData(null, "ENDİRİM 20%", 20.0)]   // kateqoriya yoxdur → endirim yoxdur
    [InlineData("cat", "VIP", null)]
    [InlineData("cat", null, null)]
    public void PosDiscount_Percent_Null(string? id, string? name, double? stored)
        => PosDiscount.Percent(id, name, (decimal?)stored).Should().BeNull();

    [Fact]
    public void BalanceChangeText_DefaultAndPlaceholder()
    {
        new CardDesignSettings().EffectiveBalanceChangeText.Should().Be("Balansınız: %@");
        new CardDesignSettings { BalanceChangeText = "Yeni balans" }.EffectiveBalanceChangeText.Should().Be("Yeni balans %@");
        new CardDesignSettings().NotifyOnBalanceChange.Should().BeFalse();
    }

    [Fact]
    public void ApplyBalance_MergeRules()
    {
        var d = new CardDesignSettings();
        d.ApplyBalance("ədəd", 1, true, "Balans: %@");
        d.BalanceUnit.Should().Be("ədəd");
        d.BalanceFormat.Should().Be(BalanceFormat.Integer);
        d.NotifyOnBalanceChange.Should().BeTrue();

        d.ApplyBalance(null, null, null, null);   // toxunma
        d.BalanceUnit.Should().Be("ədəd");

        d.ApplyBalance("", 0, false, "");
        d.BalanceUnit.Should().BeNull();
        d.BalanceFormat.Should().BeNull();
        d.BalanceNotify.Should().BeNull();
        d.BalanceChangeText.Should().BeNull();

        var json = new CardDesignSettings { BalanceUnit = "ədəd", BalanceFormat = BalanceFormat.Integer }.ToJson();
        CardDesignSettings.Parse(json).BalanceFormat.Should().Be(BalanceFormat.Integer);
    }

    [Fact]
    public void NotifiesOnChange_OnlyBalanceAndFreeReward()
    {
        CardFieldCatalog.NotifiesOnChange(CardFieldCatalog.Balance).Should().BeTrue();
        CardFieldCatalog.NotifiesOnChange(CardFieldCatalog.FreeReward).Should().BeTrue();
        CardFieldCatalog.NotifiesOnChange(CardFieldCatalog.Discount).Should().BeFalse();
    }
}

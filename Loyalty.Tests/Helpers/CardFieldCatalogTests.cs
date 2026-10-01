using FluentAssertions;
using Loyalty.Business.Dtos.Post.Main;
using Loyalty.Business.Helpers;
using Loyalty.DataAccess.Enums;

namespace Loyalty.Tests.Helpers;

/// <summary>Faza 2 — sahə kataloqu: default yerləşmə (= bugünkü görünüş), limitlər, Google sətir törəməsi, dəyərlər, JSON v1→v2.</summary>
public class CardFieldCatalogTests
{
    private static CardTemplateSnapshot Points(bool payMentType = false) => new()
    {
        TemplateType = CardTemplateType.PointsBased, Currency = "AZN", BronzePercent = 5, BronzePercentCash = 7,
        ProgramName = "Pubzade Loyalty", PayMentType = payMentType, Design = new CardDesignSettings()
    };

    private static CardTemplateSnapshot Tier() => new()
    {
        TemplateType = CardTemplateType.TierBased, Currency = "AZN",
        BronzePercent = 5, SilverThreshold = 100, SilverPercent = 10, GoldThreshold = 200, GoldPercent = 15, PlatinumThreshold = 300, PlatinumPercent = 20,
        Design = new CardDesignSettings()
    };

    private static CardTemplateSnapshot Progress() => new()
    {
        TemplateType = CardTemplateType.ProgressBased, RewardDescription = "Get 1 free", FreeRewardLabel = "Pulsuz içki", Design = new CardDesignSettings()
    };

    [Fact]
    public void DefaultFields_Points_MatchesTodayLayout()
    {
        var f = CardFieldCatalog.DefaultFields(CardTemplateType.PointsBased).ToDictionary(x => x.Id);
        f[CardFieldCatalog.Balance].Slot.Should().Be(CardFieldSlot.Header);
        f[CardFieldCatalog.CustomerName].Slot.Should().Be(CardFieldSlot.Secondary);
        f[CardFieldCatalog.Cashback].Slot.Should().Be(CardFieldSlot.Auxiliary);
        f.Values.Where(x => x.Slot != CardFieldSlot.Hidden).Should().HaveCount(3);
        f.Keys.Should().BeEquivalentTo(CardFieldCatalog.AllIds);
    }

    [Fact]
    public void DefaultFields_Progress_MatchesTodayLayout()
    {
        var f = CardFieldCatalog.DefaultFields(CardTemplateType.ProgressBased).ToDictionary(x => x.Id);
        f[CardFieldCatalog.RewardRule].Slot.Should().Be(CardFieldSlot.Primary);
        f[CardFieldCatalog.CustomerName].Slot.Should().Be(CardFieldSlot.Secondary);
        f[CardFieldCatalog.FreeReward].Slot.Should().Be(CardFieldSlot.Secondary);
        f[CardFieldCatalog.FreeReward].Order.Should().BeGreaterThan(f[CardFieldCatalog.CustomerName].Order);
        f[CardFieldCatalog.Balance].Slot.Should().Be(CardFieldSlot.Hidden);
    }

    [Fact]
    public void Effective_NullSaved_EqualsDefaults()
    {
        var eff = CardFieldCatalog.Effective(null, CardTemplateType.PointsBased, out var w);
        w.Should().BeEmpty();
        eff.Where(f => f.Slot != CardFieldSlot.Hidden).Select(f => f.Id)
            .Should().BeEquivalentTo(new[] { CardFieldCatalog.Balance, CardFieldCatalog.CustomerName, CardFieldCatalog.Cashback });
    }

    [Fact]
    public void Effective_SavedPartial_UnlistedHidden_UnknownIgnored_NotApplicableHidden()
    {
        var saved = new List<CardFieldSetting>
        {
            new() { Id = CardFieldCatalog.CustomerName, Slot = CardFieldSlot.Header, Order = 1, Label = " Müştəri " },
            new() { Id = CardFieldCatalog.Tier, Slot = CardFieldSlot.Secondary, Order = 1 },   // PointsBased-də tətbiq olunmur
            new() { Id = "nope", Slot = CardFieldSlot.Primary, Order = 1 },
        };
        var eff = CardFieldCatalog.Effective(saved, CardTemplateType.PointsBased, out var w);
        w.Should().BeEmpty();
        eff.Should().HaveCount(CardFieldCatalog.AllIds.Length);
        var byId = eff.ToDictionary(f => f.Id);
        byId[CardFieldCatalog.CustomerName].Slot.Should().Be(CardFieldSlot.Header);
        byId[CardFieldCatalog.CustomerName].Label.Should().Be("Müştəri");
        byId[CardFieldCatalog.Balance].Slot.Should().Be(CardFieldSlot.Hidden);  // siyahıda yoxdur → gizli
        byId[CardFieldCatalog.Tier].Slot.Should().Be(CardFieldSlot.Hidden);     // tipə uyğun deyil
        eff.Should().NotContain(f => f.Id == "nope");
    }

    [Fact]
    public void Effective_SlotLimits_OverflowGoesToBack_WithWarning()
    {
        var saved = new List<CardFieldSetting>
        {
            new() { Id = CardFieldCatalog.Balance, Slot = CardFieldSlot.Header, Order = 1 },
            new() { Id = CardFieldCatalog.CustomerName, Slot = CardFieldSlot.Header, Order = 2 },
            new() { Id = CardFieldCatalog.Cashback, Slot = CardFieldSlot.Header, Order = 3 },
            new() { Id = CardFieldCatalog.CardNumber, Slot = CardFieldSlot.Header, Order = 4 },
            new() { Id = CardFieldCatalog.ProgramName, Slot = CardFieldSlot.Primary, Order = 1 },
            new() { Id = CardFieldCatalog.LifetimePoints, Slot = CardFieldSlot.Primary, Order = 2 },
        };
        var eff = CardFieldCatalog.Effective(saved, CardTemplateType.PointsBased, out var w).ToDictionary(f => f.Id);
        eff[CardFieldCatalog.CardNumber].Slot.Should().Be(CardFieldSlot.Back);
        eff[CardFieldCatalog.LifetimePoints].Slot.Should().Be(CardFieldSlot.Back);
        eff[CardFieldCatalog.ProgramName].Slot.Should().Be(CardFieldSlot.Primary);
        w.Should().HaveCount(2);
    }

    [Fact]
    public void GoogleRows_PointsDefault_TopBanner()
    {
        var eff = CardFieldCatalog.Effective(null, CardTemplateType.PointsBased, out _);
        var rows = CardFieldCatalog.GoogleRows(eff, GoogleBannerPosition.Top, out var w);
        w.Should().BeEmpty();
        rows.Should().BeEquivalentTo(new List<List<string>>
        {
            new() { CardFieldCatalog.GoogleImageRow },
            new() { CardFieldCatalog.Balance },
            new() { CardFieldCatalog.CustomerName },
            new() { CardFieldCatalog.Cashback },
        }, o => o.WithStrictOrdering());
    }

    [Fact]
    public void GoogleRows_BottomBanner_NoImageRow_BackExcluded_RowItemLimit()
    {
        var saved = new List<CardFieldSetting>
        {
            new() { Id = CardFieldCatalog.Balance, Slot = CardFieldSlot.Header, Order = 1 },
            new() { Id = CardFieldCatalog.ProgramName, Slot = CardFieldSlot.Primary, Order = 1 },
            new() { Id = CardFieldCatalog.CustomerName, Slot = CardFieldSlot.Secondary, Order = 2 },
            new() { Id = CardFieldCatalog.Cashback, Slot = CardFieldSlot.Secondary, Order = 1 },
            new() { Id = CardFieldCatalog.CardNumber, Slot = CardFieldSlot.Secondary, Order = 3 },
            new() { Id = CardFieldCatalog.LifetimePoints, Slot = CardFieldSlot.Secondary, Order = 4 },
            new() { Id = CardFieldCatalog.PointsExpiry, Slot = CardFieldSlot.Back, Order = 1 },
        };
        var eff = CardFieldCatalog.Effective(saved, CardTemplateType.PointsBased, out _);
        var rows = CardFieldCatalog.GoogleRows(eff, GoogleBannerPosition.Bottom, out var w);
        rows.Should().HaveCount(2);
        rows[0].Should().Equal(CardFieldCatalog.Balance, CardFieldCatalog.ProgramName);          // Header+Primary, Order ilə
        rows[1].Should().Equal(CardFieldCatalog.Cashback, CardFieldCatalog.CustomerName, CardFieldCatalog.CardNumber); // ≤3, sıra ilə
        w.Should().ContainSingle(x => x.Contains(CardFieldCatalog.LifetimePoints));
    }

    [Fact]
    public void BuildValues_PlatformDifferences_MatchLegacy()
    {
        var apple = CardFieldCatalog.BuildValues(Progress(), "Ad Soyad", "789", 0, 3, 2, 0, null, CardPlatform.Apple);
        apple[CardFieldCatalog.RewardRule].Value.Should().Be("GET 1 FREE");
        apple[CardFieldCatalog.RewardRule].DefaultLabel.Should().BeNull();
        apple[CardFieldCatalog.FreeReward].Value.Should().Be("Pulsuz içki: 2");
        apple[CardFieldCatalog.FreeReward].DefaultLabel.Should().BeNull();
        apple[CardFieldCatalog.CustomerName].DefaultLabel.Should().BeNull(); // progress: etiketsiz (bugünkü kimi)

        var google = CardFieldCatalog.BuildValues(Progress(), "Ad Soyad", "789", 0, 3, 2, 0, null, CardPlatform.Google);
        google[CardFieldCatalog.RewardRule].Value.Should().Be("Get 1 free");
        google[CardFieldCatalog.FreeReward].Value.Should().Be("2");
        google[CardFieldCatalog.FreeReward].DefaultLabel.Should().Be("Pulsuz içki");
    }

    [Fact]
    public void BuildValues_Points_BalanceCashbackLabels_AndPayMentType()
    {
        var v = CardFieldCatalog.BuildValues(Points(), "Ad Soyad", "789", 12.5m, 0, 0, 240m, "31.12.2026 23:59", CardPlatform.Apple);
        v[CardFieldCatalog.Balance].Value.Should().Be("12.5 AZN");
        v[CardFieldCatalog.Balance].DefaultLabel.Should().Be("Balance");
        v[CardFieldCatalog.CustomerName].DefaultLabel.Should().Be("Customer");
        v[CardFieldCatalog.Cashback].Value.Should().Be("5%");
        v[CardFieldCatalog.Cashback].DefaultLabel.Should().Be("Cashback");
        v[CardFieldCatalog.LifetimePoints].Value.Should().Be("240 AZN");
        v[CardFieldCatalog.PointsExpiry].Value.Should().Be("31.12.2026 23:59");
        v[CardFieldCatalog.Tier].Value.Should().BeEmpty();            // PointsBased → tier yoxdur → göstərilmir
        v[CardFieldCatalog.CardNumber].Value.Should().Be("789");

        var pay = CardFieldCatalog.BuildValues(Points(payMentType: true), "A", "1", 0, 0, 0, 0, null, CardPlatform.Google);
        pay[CardFieldCatalog.Cashback].DefaultLabel.Should().Be("Cashback:5%");
        pay[CardFieldCatalog.Cashback].Value.Should().Be(" Nağd ödənişdə 7%");
    }

    [Fact]
    public void Tier_PercentsAndName_ByThreshold()
    {
        var t = Tier();
        CardFieldCatalog.CashbackPercents(t, 50).Percent.Should().Be(5);
        CardFieldCatalog.CashbackPercents(t, 150).Percent.Should().Be(10);
        CardFieldCatalog.CashbackPercents(t, 250).Percent.Should().Be(15);
        CardFieldCatalog.CashbackPercents(t, 999).Percent.Should().Be(20);
        CardFieldCatalog.TierName(t, 250).Should().Be("Gold");
        CardFieldCatalog.BuildValues(t, "A", "1", 250, 0, 0, 0, null, CardPlatform.Apple)[CardFieldCatalog.Tier].Value.Should().Be("Gold");
    }

    [Fact]
    public void EffectiveLabel_OverrideKeepsPayMentSuffix()
    {
        var s = Points(payMentType: true);
        var v = CardFieldCatalog.BuildValues(s, "A", "1", 0, 0, 0, 0, null, CardPlatform.Apple);
        var f = new CardFieldSetting { Id = CardFieldCatalog.Cashback, Slot = CardFieldSlot.Auxiliary, Label = "Keşbek" };
        CardFieldCatalog.EffectiveLabel(f, v[CardFieldCatalog.Cashback], s, 0).Should().Be("Keşbek:5%");
        var g = new CardFieldSetting { Id = CardFieldCatalog.Balance, Slot = CardFieldSlot.Header, Label = "Balans" };
        CardFieldCatalog.EffectiveLabel(g, v[CardFieldCatalog.Balance], s, 0).Should().Be("Balans");
        var h = new CardFieldSetting { Id = CardFieldCatalog.Balance, Slot = CardFieldSlot.Header };
        CardFieldCatalog.EffectiveLabel(h, v[CardFieldCatalog.Balance], s, 0).Should().Be("Balance");
    }

    [Theory]
    [InlineData(null, "PKBarcodeFormatCode128", "CODE_128")]
    [InlineData(BarcodeType.Code128, "PKBarcodeFormatCode128", "CODE_128")]
    [InlineData(BarcodeType.QR, "PKBarcodeFormatQR", "QR_CODE")]
    [InlineData(BarcodeType.PDF417, "PKBarcodeFormatPDF417", "PDF_417")]
    [InlineData(BarcodeType.Aztec, "PKBarcodeFormatAztec", "AZTEC")]
    public void BarcodeMapping(BarcodeType? t, string apple, string google)
    {
        CardFieldCatalog.AppleBarcodeFormat(t).Should().Be(apple);
        CardFieldCatalog.GoogleBarcodeType(t).Should().Be(google);
    }

    [Fact]
    public void ModuleIds_MatchLegacyClassRows()
    {
        CardFieldCatalog.GoogleModuleId(CardFieldCatalog.CustomerName, CardTemplateType.ProgressBased).Should().Be("rewardCard");
        CardFieldCatalog.GoogleModuleId(CardFieldCatalog.CustomerName, CardTemplateType.PointsBased).Should().Be("customer");
        CardFieldCatalog.GoogleModuleId(CardFieldCatalog.FreeReward, CardTemplateType.ProgressBased).Should().Be("pulsuzIcki");
        CardFieldCatalog.GoogleModuleId(CardFieldCatalog.RewardRule, CardTemplateType.ProgressBased).Should().Be("rewardDesc");
        CardFieldCatalog.GoogleModuleId(CardFieldCatalog.Balance, CardTemplateType.PointsBased).Should().Be("balance");
        CardFieldCatalog.AppleKey(CardFieldCatalog.CustomerName, CardTemplateType.PointsBased).Should().Be("name");
        CardFieldCatalog.AppleKey(CardFieldCatalog.CustomerName, CardTemplateType.ProgressBased).Should().Be("rewardCard");
    }

    // ---------- CardDesignSettings v2 ----------

    [Fact]
    public void DesignSettings_V1Json_NoLayout_UsesDefaults()
    {
        var d = CardDesignSettings.Parse("{\"version\":1,\"title\":\"Pubzade\",\"balanceLabel\":\"Balans\"}");
        d.Fields.Should().BeNull();
        d.Barcode.Should().BeNull();
        d.EffectiveBarcode.Should().Be(BarcodeType.Code128);
        d.EffectiveGoogleBanner(CardTemplateType.PointsBased).Should().Be(GoogleBannerPosition.Top);
        d.EffectiveGoogleBanner(CardTemplateType.ProgressBased).Should().Be(GoogleBannerPosition.Bottom);
        d.EffectiveFields(CardTemplateType.PointsBased).First(f => f.Id == CardFieldCatalog.Balance).Slot.Should().Be(CardFieldSlot.Header);
    }

    [Fact]
    public void DesignSettings_ApplyLayout_RoundTripsJson()
    {
        var d = new CardDesignSettings { Title = "T" };
        var layout = CardLayoutUpdateDto.Parse(
            "{\"barcode\":1,\"googleBanner\":0,\"fields\":[{\"id\":\"balance\",\"slot\":1,\"order\":1,\"label\":\"Balans\"},{\"id\":\"cashback\",\"slot\":0,\"order\":9}]}",
            out var err);
        err.Should().BeNull();
        d.ApplyLayout(layout!);
        d.Barcode.Should().Be(BarcodeType.QR);
        d.GoogleBanner.Should().Be(GoogleBannerPosition.Bottom);
        d.Fields.Should().HaveCount(2);

        var json = d.ToJson();
        json.Should().Contain("\"fields\":[").And.Contain("\"barcode\":1").And.Contain("\"googleBanner\":0");
        var back = CardDesignSettings.Parse(json);
        back.Version.Should().Be(2);
        back.EffectiveBarcode.Should().Be(BarcodeType.QR);
        var eff = back.EffectiveFields(CardTemplateType.PointsBased).ToDictionary(f => f.Id);
        eff[CardFieldCatalog.Balance].Slot.Should().Be(CardFieldSlot.Header);
        eff[CardFieldCatalog.Balance].Label.Should().Be("Balans");
        eff[CardFieldCatalog.Cashback].Slot.Should().Be(CardFieldSlot.Hidden);
        eff[CardFieldCatalog.CustomerName].Slot.Should().Be(CardFieldSlot.Hidden); // siyahıda yoxdur → gizli
        back.Title.Should().Be("T");
    }

    [Fact]
    public void DesignSettings_ApplyLayout_ZeroBarcodeResets_EmptyFieldsResets()
    {
        var d = new CardDesignSettings { Barcode = BarcodeType.Aztec, GoogleBanner = GoogleBannerPosition.None, Fields = new() { new() { Id = "balance", Slot = CardFieldSlot.Back } } };
        d.ApplyLayout(new CardLayoutUpdateDto { Barcode = 0, GoogleBanner = -1, Fields = new() });
        d.Barcode.Should().BeNull();
        d.GoogleBanner.Should().BeNull();
        d.Fields.Should().BeNull();
        // null → toxunma
        d.Barcode = BarcodeType.PDF417;
        d.ApplyLayout(new CardLayoutUpdateDto());
        d.Barcode.Should().Be(BarcodeType.PDF417);
    }

    [Theory]
    [InlineData("{\"fields\":[{\"id\":\"balance\",\"slot\":7}]}", "yuva")]
    [InlineData("{\"fields\":[{\"id\":\"balance\",\"slot\":1},{\"id\":\"balance\",\"slot\":2}]}", "təkrarlanır")]
    [InlineData("{\"barcode\":9}", "Barkod")]
    [InlineData("{\"googleBanner\":5}", "banner")]
    [InlineData("{bad json", "oxunmadı")]
    public void LayoutDto_Parse_Invalid(string json, string errorPart)
    {
        var dto = CardLayoutUpdateDto.Parse(json, out var err);
        dto.Should().BeNull();
        err.Should().Contain(errorPart);
    }

    [Fact]
    public void LayoutDto_Parse_EmptyIsNull_NoError()
    {
        CardLayoutUpdateDto.Parse("", out var err).Should().BeNull();
        err.Should().BeNull();
    }

    // ---------- Köhnə (bugünkü) Google yerləşməsi — "Sahələr" toxunulmayıbsa dəyişmir ----------

    [Fact]
    public void LegacyGoogleRows_MatchProdClasses()
    {
        CardFieldCatalog.LegacyGoogleRows(CardTemplateType.PointsBased).Should().BeEquivalentTo(new List<List<string>>
        {
            new() { CardFieldCatalog.GoogleImageRow },
            new() { CardFieldCatalog.CustomerName, CardFieldCatalog.Balance },
            new() { CardFieldCatalog.Cashback },
            new() { CardFieldCatalog.GoogleBarcodeRow },
        }, o => o.WithStrictOrdering());
        CardFieldCatalog.LegacyGoogleRows(CardTemplateType.ProgressBased).Should().BeEquivalentTo(new List<List<string>>
        {
            new() { CardFieldCatalog.CustomerName }, new() { CardFieldCatalog.FreeReward }, new() { CardFieldCatalog.GoogleBarcodeRow },
            new() { CardFieldCatalog.RewardRule }, new() { CardFieldCatalog.GoogleImageRow },
        }, o => o.WithStrictOrdering());
        CardFieldCatalog.LegacyGoogleRows(CardTemplateType.TierBased).Should().HaveCount(4);
    }

    [Fact]
    public void IsLegacyGoogleLayout_OnlyWhenFieldsAndBannerUntouched()
    {
        CardFieldCatalog.IsLegacyGoogleLayout(null).Should().BeTrue();
        CardFieldCatalog.IsLegacyGoogleLayout(new CardDesignSettings()).Should().BeTrue();
        CardFieldCatalog.IsLegacyGoogleLayout(new CardDesignSettings { Fields = new() }).Should().BeTrue();
        CardFieldCatalog.IsLegacyGoogleLayout(new CardDesignSettings { Barcode = BarcodeType.QR }).Should().BeTrue();   // barkod tipi sətirləri dəyişmir
        CardFieldCatalog.IsLegacyGoogleLayout(new CardDesignSettings { GoogleBanner = GoogleBannerPosition.Bottom }).Should().BeFalse();
        CardFieldCatalog.IsLegacyGoogleLayout(new CardDesignSettings { Fields = new() { new() { Id = "balance", Slot = CardFieldSlot.Header } } }).Should().BeFalse();
        // "default-a qaytar" (ApplyLayout fields []) → yenidən legacy
        var d = new CardDesignSettings { Fields = new() { new() { Id = "balance", Slot = CardFieldSlot.Header } }, GoogleBanner = GoogleBannerPosition.Top };
        d.ApplyLayout(new CardLayoutUpdateDto { Fields = new(), GoogleBanner = -1 });
        CardFieldCatalog.IsLegacyGoogleLayout(d).Should().BeTrue();
    }
}

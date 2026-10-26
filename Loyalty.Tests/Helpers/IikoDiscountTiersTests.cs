using FluentAssertions;
using Loyalty.Business.Helpers;
using Loyalty.Business.Services.Abstractions.Main;

namespace Loyalty.Tests.Helpers;

/// <summary>Carpax "ENDİRİM 20%" / "Endirim 30%": OrderPercentDiscountAction + GuestCategoryCondition (+ ManualCondition) → endirim səviyyələri.</summary>
public class IikoDiscountTiersTests
{
    private const string Cat20 = "10ef8a6c-1617-4c15-80ca-45ccefc86e2e";
    private const string Cat30 = "6d8b7eb2-3ef8-412e-bc73-b0ece2999bd3";

    private static IikoRuleItem Rule(string type, string settings) => new() { Id = Guid.NewGuid(), TypeName = type, Settings = settings };

    private static IikoActionConditionBinding Bind(IEnumerable<IikoRuleItem> actions, IEnumerable<IikoRuleItem>? conditions = null)
        => new() { Id = Guid.NewGuid(), Actions = actions.ToList(), Conditions = (conditions ?? Array.Empty<IikoRuleItem>()).ToList() };

    /// <summary>Real Carpax JSON-dan (2026-10-26) qısaldılmış kampaniya.</summary>
    private static IikoMarketingCampaign CarpaxDiscount(string name, decimal percent, string categoryId, bool manual, bool active = true)
    {
        var conditions = new List<IikoRuleItem>
        {
            Rule("LoyaltyProgramEngine.Conditions.GuestCategoryCondition", $$"""{"GuestCategoryIds":["{{categoryId}}"],"AffectedGuest":0}""")
        };
        if (manual)
            conditions.Add(Rule("LoyaltyProgramEngine.Conditions.ManualCondition", $$"""{"Caption":"{{name}}","MarketingCampaignId":"7dd50000-64fa-c2b3-9a4b-08df2c52635a","CnoId":null}"""));
        return new IikoMarketingCampaign
        {
            Id = Guid.NewGuid(), Name = name, IsActive = active, PeriodFrom = new DateTime(2026, 10, 2),
            OrderActionConditionBindings = new()
            {
                Bind(new[] { Rule("LoyaltyProgramEngine.Actions.OrderPercentDiscountAction",
                    $$"""{"DiscountPercent":{{percent.ToString(System.Globalization.CultureInfo.InvariantCulture)}},"AppliedToDiscountedItems":true}""") }, conditions)
            },
            OverdraftActionConditionBindings = new()
            {
                Bind(new[] { Rule("LoyaltyProgramEngine.Actions.Payment.OverdraftAction", """{"Mode":2,"Sum":0.0}""") })
            }
        };
    }

    [Fact]
    public void Parser_OrderPercentDiscount_WithGuestCategory_AndManual()
    {
        var c = IikoRuleParser.SummarizeCampaign(CarpaxDiscount("ENDİRİM 20%", 20m, Cat20, manual: true));

        c.Kind.Should().Be(IikoRuleParser.KindDiscountPercent);
        c.KindLabel.Should().Be("Sifarişə endirim %");
        c.Percent.Should().Be(20m);
        c.GuestCategoryIds.Should().ContainSingle().Which.Should().Be(Cat20);
        c.RequiresManualActivation.Should().BeTrue();
        c.Conditions.Select(x => x.Text).Should().Contain(t => t.StartsWith("Kassir əl ilə seçir"));
    }

    [Fact]
    public void Parser_WithoutManualCondition_IsAutomatic()
    {
        var c = IikoRuleParser.SummarizeCampaign(CarpaxDiscount("Endirim 30%", 30m, Cat30, manual: false));
        c.RequiresManualActivation.Should().BeFalse();
        c.Percent.Should().Be(30m);
    }

    [Fact]
    public void Parser_OtherPercentDiscountAction_FallsBackToDiscountKind()
    {
        var mc = new IikoMarketingCampaign
        {
            Id = Guid.NewGuid(), Name = "Məhsul endirimi", IsActive = true,
            OrderActionConditionBindings = new()
            {
                Bind(new[] { Rule("LoyaltyProgramEngine.Actions.ProductPercentDiscountAction", """{"DiscountPercent":15.0,"MaskItems":[]}""") })
            }
        };
        var c = IikoRuleParser.SummarizeCampaign(mc);
        c.Kind.Should().Be(IikoRuleParser.KindDiscountPercent);
        c.Percent.Should().Be(15m);
    }

    private static IikoCatalogProgramDto Program(params IikoMarketingCampaign[] campaigns) => new()
    {
        Id = Guid.NewGuid().ToString(), Name = "Cashback", IsActive = true,
        Campaigns = campaigns.Select(mc => IikoRuleParser.SummarizeCampaign(mc)).ToList()
    };

    private static List<IikoCatalogGuestCategoryDto> Cats() => new()
    {
        new() { Id = Cat20, Name = "Endirim 20", IsActive = true },
        new() { Id = Cat30, Name = "Endirim 30", IsActive = true }
    };

    [Fact]
    public void Build_Carpax_TwoTiers_SortedByPercentDesc()
    {
        var program = Program(
            CarpaxDiscount("ENDİRİM 20%", 20m, Cat20, manual: true),
            CarpaxDiscount("Endirim 30%", 30m, Cat30, manual: true),
            new IikoMarketingCampaign
            {
                Id = Guid.NewGuid(), Name = "Оплата", IsActive = true,
                OrderActionConditionBindings = new()
                {
                    Bind(new[] { Rule("LoyaltyProgramEngine.Actions.PaymentAction", """{"WalletId":"d0b10000-8bc3-62f6-8657-08df209041fa","MaskItems":[],"WhatToPay":0}""") })
                }
            });

        var tiers = IikoDiscountTiers.Build(new[] { program }, Cats());

        tiers.Should().HaveCount(2);
        tiers[0].Percent.Should().Be(30m);
        tiers[0].CampaignName.Should().Be("Endirim 30%");
        tiers[0].GuestCategoryId.Should().Be(Cat30);
        tiers[0].GuestCategoryName.Should().Be("Endirim 30");
        tiers[0].RequiresManualActivation.Should().BeTrue();
        tiers[1].Percent.Should().Be(20m);
        tiers[1].GuestCategoryId.Should().Be(Cat20);
    }

    [Fact]
    public void Build_SkipsInactive_NoCategory_ZeroPercent_AndDuplicates()
    {
        var noCategory = new IikoMarketingCampaign
        {
            Id = Guid.NewGuid(), Name = "Hamıya 10%", IsActive = true,
            OrderActionConditionBindings = new()
            {
                Bind(new[] { Rule("LoyaltyProgramEngine.Actions.OrderPercentDiscountAction", """{"DiscountPercent":10.0}""") })
            }
        };
        var program = Program(
            CarpaxDiscount("Köhnə 25%", 25m, Cat20, manual: false, active: false),
            CarpaxDiscount("ENDİRİM 20%", 20m, Cat20, manual: false),
            CarpaxDiscount("Dublikat 20%", 20m, Cat20, manual: false),
            CarpaxDiscount("Sıfır", 0m, Cat30, manual: false),
            noCategory);

        var tiers = IikoDiscountTiers.Build(new[] { program }, new List<IikoCatalogGuestCategoryDto>());

        tiers.Should().ContainSingle();
        tiers[0].CampaignName.Should().Be("ENDİRİM 20%");
        tiers[0].GuestCategoryName.Should().BeNull();
        tiers[0].RequiresManualActivation.Should().BeFalse();
    }
}

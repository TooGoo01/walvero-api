using FluentAssertions;
using Loyalty.Business.Helpers;
using Loyalty.Business.Services.Abstractions.Main;

namespace Loyalty.Tests.Helpers;

/// <summary>Cupcup iiko proqramları (prod, 2026-10-26) — "N al → 1 pulsuz" desert və içki kampaniyaları; məhsul siyahıları qısaldılıb.</summary>
internal static class CupcupIikoFixture
{
    public const string DessertWallet = "03650000-6bec-ac1f-f0fb-08de310116ea";
    public const string DrinkWallet = "01330000-6bec-ac1f-25e5-08dd2b7b7a8e";
    public static readonly Guid DessertProgramId = Guid.Parse("03650000-6bec-ac1f-aaaa-08de310116ea");
    public static readonly Guid DrinkProgramId = Guid.Parse("01330000-6bec-ac1f-bbbb-08dd2b7b7a8e");
    public static readonly Guid DessertGiftCampaignId = Guid.Parse("03650000-6bec-ac1f-04c4-08de31015f8b");

    public static IikoRuleItem Rule(string type, string settings) => new() { Id = Guid.NewGuid(), TypeName = type, Settings = settings };

    public static IikoActionConditionBinding Bind(IEnumerable<IikoRuleItem> actions, IEnumerable<IikoRuleItem>? conditions = null)
        => new() { Id = Guid.NewGuid(), Actions = actions.ToList(), Conditions = (conditions ?? Array.Empty<IikoRuleItem>()).ToList() };

    private static IikoActionConditionBinding Overdraft()
        => Bind(new[] { Rule("LoyaltyProgramEngine.Actions.Payment.OverdraftAction", """{"Mode":2,"Sum":0.0}""") });

    private static string FixedBonus(string wallet) =>
        $$"""{"BonusValue":1.0,"WalletId":"{{wallet}}","ChequeText":"Счет будет пополнен на @Model.SumWithCurrency","Sms":null,"MaxSupplementsPerDay":null,"AffectedGuest":0}""";

    public const string DessertEarnCondition =
        """{"CheckedType":2,"MaskItems":[{"Size":"","MaskValue":"DESERT","MaskType":3,"UnitsPrice":1},{"Size":"","MaskValue":"02004","MaskType":1,"UnitsPrice":1},{"Size":"","MaskValue":"02006","MaskType":1,"UnitsPrice":1}],"MultiplicityEnabled":true,"IgnoreFreeItems":false,"ItemsCount":1}""";

    public const string DessertGift =
        """{"IncludeModifiers":true,"Caption":"","Items":[{"Code":"01755","Sizes":[],"DisplayName":""},{"Code":"01753","Sizes":[],"DisplayName":""},{"Code":"02004","Sizes":[],"DisplayName":""}],"WithdrawFromWalletOnUse":true,"WithdrawalMultiplier":7}""";

    public const string DrinkGift =
        """{"IncludeModifiers":false,"Caption":"","Items":[{"Code":"*","Sizes":["SMALL","MEDIUM","LARGE"],"DisplayName":""},{"Code":"01707","Sizes":[],"DisplayName":""}],"WithdrawFromWalletOnUse":true,"WithdrawalMultiplier":10}""";

    private static IikoRuleItem Manual(string caption, Guid campaignId) =>
        Rule("LoyaltyProgramEngine.Conditions.ManualCondition", $$"""{"Caption":"{{caption}}","MarketingCampaignId":"{{campaignId}}","CnoId":null}""");

    private static IikoRuleItem Balance(string wallet, int sum) =>
        Rule("LoyaltyProgramEngine.Conditions.WalletBalanceCondition", $$"""{"WalletId":"{{wallet}}","Sum":{{sum}}.0,"Operation":4,"MultiplicityEnabled":false}""");

    private static IikoRuleItem Present(string settings) => Rule("LoyaltyProgramEngine.Actions.PresentBonusAction", settings);

    public static IikoLoyaltyProgramDetail DessertProgram() => new()
    {
        Id = DessertProgramId,
        Name = "Программа для десертов(особо одаренным не трогать) ",
        IsActive = true,
        WalletId = Guid.Parse(DessertWallet),
        MarketingCampaigns = new()
        {
            new IikoMarketingCampaign
            {
                Id = Guid.NewGuid(), Name = "Накопление десертов", IsActive = true,
                OrderActionConditionBindings = new()
                {
                    Bind(new[] { Rule("LoyaltyProgramEngine.Actions.AdditionalBonusFixedSumAction", FixedBonus(DessertWallet)) },
                         new[] { Rule("LoyaltyProgramEngine.Conditions.ProductInOrderCondition", DessertEarnCondition) })
                },
                OverdraftActionConditionBindings = new() { Overdraft() }
            },
            new IikoMarketingCampaign
            {
                Id = DessertGiftCampaignId, Name = "Оплата со счета", IsActive = true,
                OrderActionConditionBindings = new()
                {
                    Bind(new[]
                        {
                            Rule("LoyaltyProgramEngine.Actions.UpsaleAction", """{"Items":[],"Suggestion":"Pulsuz desert təklif edin"}"""),
                            Present(DessertGift)
                        },
                        new[] { Manual("Pulsuz 1 dessert", DessertGiftCampaignId), Balance(DessertWallet, 7) })
                },
                OverdraftActionConditionBindings = new() { Overdraft() }
            }
        }
    };

    public static IikoLoyaltyProgramDetail DrinkProgram() => new()
    {
        Id = DrinkProgramId,
        Name = "Продуктовая программа",
        IsActive = true,
        WalletId = Guid.Parse(DrinkWallet),
        MarketingCampaigns = new()
        {
            DrinkGiftCampaign("Üç pulsuz içki", 3, 30),
            DrinkGiftCampaign("Iki pulsuz içki", 2, 20),
            new IikoMarketingCampaign
            {
                Id = Guid.NewGuid(), Name = "Пополнение", IsActive = true,
                OrderActionConditionBindings = new()
                {
                    Bind(new[] { Rule("LoyaltyProgramEngine.Actions.AdditionalBonusFixedSumAction", FixedBonus(DrinkWallet)) },
                         new[] { Rule("LoyaltyProgramEngine.Conditions.ProductInOrderCondition",
                             """{"CheckedType":2,"MaskItems":[{"Size":"","MaskValue":"HOT BAR;COLD BAR","MaskType":3,"UnitsPrice":1},{"Size":"","MaskValue":"00023","MaskType":1,"UnitsPrice":1}],"MultiplicityEnabled":true,"IgnoreFreeItems":false,"ItemsCount":1}""") })
                }
            },
            DrinkGiftCampaign("Bir pulsuz içki", 1, 10),
        }
    };

    private static IikoMarketingCampaign DrinkGiftCampaign(string name, int gifts, int balance)
    {
        var id = Guid.NewGuid();
        return new IikoMarketingCampaign
        {
            Id = id, Name = name, IsActive = true,
            OrderActionConditionBindings = new()
            {
                Bind(Enumerable.Range(0, gifts).Select(_ => Present(DrinkGift)), new[] { Balance(DrinkWallet, balance), Manual(name, id) })
            }
        };
    }
}

public class IikoGiftParserTests
{
    [Fact]
    public void DessertGift_ParsedFromPresentBonusAction()
    {
        var p = IikoRuleParser.Summarize(CupcupIikoFixture.DessertProgram());
        var gift = p.Campaigns.Single(c => c.Name == "Оплата со счета");

        gift.Kind.Should().Be(IikoRuleParser.KindGift);
        gift.KindLabel.Should().Be("Hədiyyə");
        gift.Gift.Should().NotBeNull();
        gift.Gift!.CostPerGift.Should().Be(7);
        gift.Gift.GiftsPerUse.Should().Be(1);
        gift.Gift.WithdrawFromWallet.Should().BeTrue();
        gift.Gift.ProductCodes.Should().Equal("01755", "01753", "02004");
        gift.Gift.AnySizedProduct.Should().BeFalse();
        gift.Gift.Caption.Should().Be("Pulsuz 1 dessert");
        gift.Gift.WalletId.Should().Be(CupcupIikoFixture.DessertWallet);
        gift.Gift.MinBalance.Should().Be(7);
        gift.Conditions.Select(c => c.Text).Should().Contain("Kassir əl ilə seçir: «Pulsuz 1 dessert»")
            .And.Contain("Cüzdan balansı ≥ 7");
    }

    [Fact]
    public void DessertEarn_ParsedAsFixedBonusPerItem()
    {
        var p = IikoRuleParser.Summarize(CupcupIikoFixture.DessertProgram());
        var earn = p.Campaigns.Single(c => c.Name == "Накопление десертов");

        earn.Kind.Should().Be(IikoRuleParser.KindFixedBonus);
        earn.KindLabel.Should().Be("Hər məhsula +1");
        earn.Gift.Should().BeNull();
        earn.FixedEarn.Should().NotBeNull();
        earn.FixedEarn!.BonusPerItem.Should().Be(1);
        earn.FixedEarn.PerItem.Should().BeTrue();
        earn.FixedEarn.WalletId.Should().Be(CupcupIikoFixture.DessertWallet);
        earn.FixedEarn.Masks.Select(m => (m.Value, m.Type)).Should().Equal(
            ("DESERT", IikoRuleParser.MaskTypeCategory), ("02004", IikoRuleParser.MaskTypeProduct), ("02006", IikoRuleParser.MaskTypeProduct));
        earn.WalletIds.Should().Contain(CupcupIikoFixture.DessertWallet);
    }

    [Fact]
    public void DrinkGift_SizeWildcard_AndGiftsPerUse()
    {
        var p = IikoRuleParser.Summarize(CupcupIikoFixture.DrinkProgram());

        var three = p.Campaigns.Single(c => c.Name == "Üç pulsuz içki").Gift!;
        three.GiftsPerUse.Should().Be(3);
        three.CostPerGift.Should().Be(10);
        three.AnySizedProduct.Should().BeTrue();
        three.ProductCodes.Should().Equal("01707");
        three.MinBalance.Should().Be(30);

        p.Campaigns.Single(c => c.Name == "Bir pulsuz içki").Gift!.GiftsPerUse.Should().Be(1);
    }

    [Fact]
    public void DrinkEarn_CategoryListSplit()
    {
        var p = IikoRuleParser.Summarize(CupcupIikoFixture.DrinkProgram());
        var earn = p.Campaigns.Single(c => c.Name == "Пополнение").FixedEarn!;

        earn.Masks.Select(m => m.Value).Should().Equal("HOT BAR", "COLD BAR", "00023");
    }

    [Fact]
    public void CashbackCampaign_HasNoGiftOrFixedEarn()
    {
        var program = new IikoLoyaltyProgramDetail
        {
            Id = Guid.NewGuid(), Name = "Cashback", IsActive = true,
            MarketingCampaigns = new()
            {
                new IikoMarketingCampaign
                {
                    Id = Guid.NewGuid(), Name = "Поплнение", IsActive = true,
                    OrderActionConditionBindings = new()
                    {
                        CupcupIikoFixture.Bind(new[] { CupcupIikoFixture.Rule("LoyaltyProgramEngine.Actions.RefillWalletPercentAction",
                            """{"RefillPercent":5.0,"WalletId":"d0b10000-8bc3-62f6-adf1-08df1ee98316","MaskItems":[]}""") })
                    }
                }
            }
        };
        var c = IikoRuleParser.Summarize(program).Campaigns.Single();
        c.Gift.Should().BeNull();
        c.FixedEarn.Should().BeNull();
        c.Kind.Should().Be(IikoRuleParser.KindCashbackPercent);
    }
}

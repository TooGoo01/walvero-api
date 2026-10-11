using FluentAssertions;
using Loyalty.Business.Helpers;
using Loyalty.Business.Services.Abstractions.Main;

namespace Loyalty.Tests.Helpers;

/// <summary>iiko marketinq kampaniyası → oxunaqlı xülasə (real Carrousel / Pims cavablarından qısaldılmış nümunələr).</summary>
public class IikoRuleParserTests
{
    private const string GuestCat = "65046020-640e-461b-9213-826af52f7312";
    private const string ChocolateCat = "bb01dd2c-1111-2222-3333-444444444444";

    private static IikoRuleItem Rule(string type, string settings) => new() { Id = Guid.NewGuid(), TypeName = type, Settings = settings };

    private static IikoActionConditionBinding Bind(IEnumerable<IikoRuleItem> actions, IEnumerable<IikoRuleItem>? conditions = null)
        => new() { Id = Guid.NewGuid(), Actions = actions.ToList(), Conditions = (conditions ?? Array.Empty<IikoRuleItem>()).ToList() };

    private static readonly string RefillPercent5 =
        """{"RefillPercent":5.0,"WalletId":"d0b10000-8bc3-62f6-adf1-08df1ee98316","ProductsFilter":0,"AffectedGuest":0,"MaskItems":[]}""";
    private static readonly string ProductAll =
        """{"CheckedType":2,"MaskItems":[{"Size":"","MaskValue":"*","MaskType":1,"UnitsPrice":1}],"MultiplicityEnabled":false,"IgnoreFreeItems":true,"ItemsCount":1}""";
    private static readonly string Overdraft0 = """{"Mode":2,"Sum":0.0}""";

    /// <summary>Carrousel: «Поплнение» 5 % + ProductInOrder "*" + overdraft 0.</summary>
    private static IikoLoyaltyProgramDetail CarrouselProgram() => new()
    {
        Id = Guid.NewGuid(),
        Name = "Cashback",
        IsActive = true,
        WalletId = Guid.Parse("d0b10000-8bc3-62f6-adf1-08df1ee98316"),
        MarketingCampaigns = new()
        {
            new IikoMarketingCampaign
            {
                Id = Guid.NewGuid(), Name = "Поплнение", IsActive = true, PeriodFrom = new DateTime(2026, 9, 30),
                OrderActionConditionBindings = new()
                {
                    Bind(new[] { Rule("LoyaltyProgramEngine.Actions.RefillWalletPercentAction", RefillPercent5) },
                         new[] { Rule("LoyaltyProgramEngine.Conditions.ProductInOrderCondition", ProductAll) })
                },
                OverdraftActionConditionBindings = new()
                {
                    Bind(new[] { Rule("LoyaltyProgramEngine.Actions.Payment.OverdraftAction", Overdraft0) })
                }
            },
            new IikoMarketingCampaign
            {
                Id = Guid.NewGuid(), Name = "Оплата", IsActive = true,
                OrderActionConditionBindings = new()
                {
                    Bind(new[] { Rule("LoyaltyProgramEngine.Actions.PaymentAction",
                        """{"WalletId":"d0b10000-8bc3-62f6-adf1-08df1ee98316","MaskItems":[],"WhatToPay":0,"PaymentLimitPercent":null}""") })
                }
            }
        }
    };

    [Fact]
    public void Carrousel_FivePercent_AllProducts()
    {
        var s = IikoRuleParser.Summarize(CarrouselProgram());

        s.Name.Should().Be("Cashback");
        s.IsActive.Should().BeTrue();
        s.Campaigns.Should().HaveCount(2);

        var refill = s.Campaigns[0];
        refill.Kind.Should().Be(IikoRuleParser.KindCashbackPercent);
        refill.KindLabel.Should().Be("Keşbek %");
        refill.Percent.Should().Be(5m);
        refill.AllProducts.Should().BeTrue();
        refill.ProductScope.Should().Be(IikoRuleParser.AllProductsText);
        refill.ProductMasks.Should().BeEmpty();
        refill.Conditions.Should().BeEmpty();                 // ProductInOrder "*" şərt kimi göstərilmir
        refill.RawActions.Select(r => r.Type).Should().Equal("RefillWalletPercentAction", "overdraft:OverdraftAction");
        refill.RawConditions.Select(r => r.Type).Should().Equal("ProductInOrderCondition");

        var pay = s.Campaigns[1];
        pay.Kind.Should().Be(IikoRuleParser.KindPayment);
        pay.Percent.Should().BeNull();
        pay.AllProducts.Should().BeTrue();                     // MaskItems boş = məhdudiyyət yoxdur

        IikoRuleParser.ProgramCashbackPercent(s).Should().Be(5m);
    }

    [Fact]
    public void Pims_WelcomeBonus_GuestCategoryCondition_NameResolved()
    {
        var program = new IikoLoyaltyProgramDetail
        {
            Id = Guid.NewGuid(), Name = "Cashback", IsActive = true,
            MarketingCampaigns = new()
            {
                new IikoMarketingCampaign
                {
                    Id = Guid.NewGuid(), Name = "Приветственный Бонус 20%", IsActive = true,
                    OrderActionConditionBindings = new()
                    {
                        Bind(new[] { Rule("LoyaltyProgramEngine.Actions.RefillWalletPercentAction", RefillPercent5.Replace("5.0", "20.0")) },
                             new[]
                             {
                                 Rule("LoyaltyProgramEngine.Conditions.GuestCategoryCondition", $$$"""{"GuestCategoryIds":["{{{GuestCat}}}"],"AffectedGuest":0}"""),
                                 Rule("LoyaltyProgramEngine.Conditions.ProductInOrderCondition", ProductAll)
                             })
                    },
                    GuestRegistrationActionConditionBindings = new()
                    {
                        Bind(new[] { Rule("LoyaltyProgramEngine.Actions.SetGuestCategoryAction", $$$"""{"GuestCategoryId":"{{{GuestCat}}}"}""") },
                             new[] { Rule("LoyaltyProgramEngine.Conditions.GuestRegistrationSourceCondition", """{"AcceptedSources":[2,3]}""") })
                    }
                },
                new IikoMarketingCampaign
                {
                    Id = Guid.NewGuid(), Name = "Пополнение", IsActive = true,
                    OrderActionConditionBindings = new()
                    {
                        Bind(new[] { Rule("LoyaltyProgramEngine.Actions.RefillWalletPercentAction", RefillPercent5) },
                             new[]
                             {
                                 Rule("LoyaltyProgramEngine.Conditions.ProductInOrderCondition", ProductAll),
                                 Rule("LoyaltyProgramEngine.Conditions.GuestNotInCategoryCondition", $$$"""{"GuestCategoryIds":["{{{GuestCat}}}"]}""")
                             }),
                        Bind(new[] { Rule("LoyaltyProgramEngine.Actions.RemoveGuestCategoryAction", $$$"""{"GuestCategoryId":"{{{GuestCat}}}"}""") },
                             new[] { Rule("LoyaltyProgramEngine.Conditions.OrdersNumberCondition", """{"OrdersNumber":1,"CheckedType":2,"Period":0}""") })
                    }
                }
            }
        };

        var s = IikoRuleParser.Summarize(program, id => id == GuestCat ? "Yeni qonaq" : null);

        var welcome = s.Campaigns[0];
        welcome.Kind.Should().Be(IikoRuleParser.KindCashbackPercent);
        welcome.Percent.Should().Be(20m);
        welcome.AllProducts.Should().BeTrue();
        welcome.Conditions.Should().ContainSingle()
            .Which.Text.Should().Be("Qonaq kateqoriyası: Yeni qonaq");
        welcome.RawActions.Select(r => r.Type).Should().Contain("guestRegistration:SetGuestCategoryAction");

        var regular = s.Campaigns[1];
        regular.Percent.Should().Be(5m);                       // ilk action faizi; RemoveGuestCategory növü əvəz etmir
        regular.Kind.Should().Be(IikoRuleParser.KindCashbackPercent);
        regular.Conditions.Select(c => c.Text).Should().Equal("Qonaq kateqoriyasında DEYİL: Yeni qonaq", "Sifariş sayı: 1");

        // Şərtsiz 5 % aktiv kampaniya yoxdur → şərtli ilk aktiv (20 %) deyil, şərtsiz axtarış boş → ilk aktiv cashback = 20
        IikoRuleParser.ProgramCashbackPercent(s).Should().Be(20m);
    }

    [Fact]
    public void CategoryMask_ResolvedToName_NotAllProducts()
    {
        var program = new IikoLoyaltyProgramDetail
        {
            Id = Guid.NewGuid(), Name = "Cashback", IsActive = true,
            MarketingCampaigns = new()
            {
                new IikoMarketingCampaign
                {
                    Id = Guid.NewGuid(), Name = "Şokoladsız 5%", IsActive = true,
                    OrderActionConditionBindings = new()
                    {
                        Bind(new[] { Rule("LoyaltyProgramEngine.Actions.RefillWalletPercentAction",
                                $$$"""{"RefillPercent":5.0,"ProductsFilter":1,"MaskItems":[{"Size":"","MaskValue":"{{{ChocolateCat}}}","MaskType":3,"UnitsPrice":1},{"MaskValue":"{{{ChocolateCat}}}","MaskType":3}]}""") })
                    }
                }
            }
        };

        var s = IikoRuleParser.Summarize(program, id => id == ChocolateCat ? "ŞOKOLAD" : null);
        var c = s.Campaigns.Single();
        c.AllProducts.Should().BeFalse();
        c.ProductScope.Should().Be("Seçilmiş məhsullar");
        c.ProductMasks.Should().ContainSingle();               // təkrar maska birləşir
        c.ProductMasks[0].Value.Should().Be(ChocolateCat);
        c.ProductMasks[0].Type.Should().Be(3);
        c.ProductMasks[0].ResolvedName.Should().Be("ŞOKOLAD");
    }

    [Fact]
    public void UnknownTypes_And_BadJson_KeptRaw()
    {
        var program = new IikoLoyaltyProgramDetail
        {
            Id = Guid.NewGuid(), Name = "X", IsActive = false,
            MarketingCampaigns = new()
            {
                new IikoMarketingCampaign
                {
                    Id = Guid.NewGuid(), Name = "Weird", IsActive = true,
                    OrderActionConditionBindings = new()
                    {
                        Bind(new[] { Rule("LoyaltyProgramEngine.Actions.SomeFancyAction", "{not json") },
                             new[] { Rule("LoyaltyProgramEngine.Conditions.OrderSumCondition", """{"MinSum":50}""") })
                    }
                },
                new IikoMarketingCampaign { Id = Guid.NewGuid(), Name = "Empty", IsActive = false }
            }
        };

        var s = IikoRuleParser.Summarize(program);

        var weird = s.Campaigns[0];
        weird.Kind.Should().Be(IikoRuleParser.KindOther);
        weird.KindLabel.Should().Be("Some Fancy Action");
        weird.ProductScope.Should().Be("Məhsul filtri yoxdur");
        weird.RawActions.Single().Settings.Should().Be("{not json");
        weird.Conditions.Single().Text.Should().Be("Sifariş məbləği ≥ 50");

        var empty = s.Campaigns[1];
        empty.KindLabel.Should().Be("Sifariş qaydası yoxdur");
        empty.RawActions.Should().BeEmpty();

        IikoRuleParser.ProgramCashbackPercent(s).Should().BeNull();
        IikoRuleParser.Short(null).Should().Be("?");
        IikoRuleParser.Short("NoDots").Should().Be("NoDots");
    }
}

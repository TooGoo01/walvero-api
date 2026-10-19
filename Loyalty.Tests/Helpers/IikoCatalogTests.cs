using FluentAssertions;
using Loyalty.Business.Helpers;
using Loyalty.Business.Services.Abstractions.Main;

namespace Loyalty.Tests.Helpers;

/// <summary>iiko kataloqu — parser-in strukturlu çıxarışı (pims ambassador nümunəsi) və org-lar üzrə birləşmə.</summary>
public class IikoCatalogTests
{
    private const string CashbackWallet = "d0b10000-8bc3-62f6-adf1-08df1ee98316";
    private const string AmbassadorWallet = "7dd50000-64fa-c2b3-6e83-08df2c4fda0c";
    private const string AmbassadorCategory = "69a52f99-ea91-456c-89f5-d2effbc28159";

    private static IikoRuleItem Rule(string type, string settings) => new() { Id = Guid.NewGuid(), TypeName = type, Settings = settings };

    private static IikoActionConditionBinding Bind(IEnumerable<IikoRuleItem> actions, IEnumerable<IikoRuleItem>? conditions = null)
        => new() { Id = Guid.NewGuid(), Actions = actions.ToList(), Conditions = (conditions ?? Array.Empty<IikoRuleItem>()).ToList() };

    // pims ambassador kampaniyası (user 2026-10-18): PaymentAction F&B 1 vahid + overdraft binding-də qonaq kateqoriyası şərti
    private static readonly string PimsPayment =
        "{\"WalletId\":\"" + AmbassadorWallet + "\",\"DayLimit\":null,\"AllowMinusBalance\":false," +
        "\"MaskItems\":[{\"MaskValue\":\"Food And Beverage\",\"MaskType\":3,\"UnitsPrice\":1}],\"WhatToPay\":1," +
        "\"PaymentLimitPercent\":null,\"ChequeSumOnFullPayment\":0,\"ExchangeRate\":1}";
    private static readonly string Overdraft = "{\"Mode\":2,\"Sum\":0}";
    private static readonly string GuestCategory = "{\"GuestCategoryIds\":[\"" + AmbassadorCategory + "\"],\"AffectedGuest\":0}";
    private static readonly string Refill5 = "{\"RefillPercent\":5.0,\"WalletId\":\"" + CashbackWallet + "\",\"MaskItems\":[]}";

    private static IikoLoyaltyProgramDetail PimsAmbassadorProgram() => new()
    {
        Id = Guid.NewGuid(),
        Name = "Ambassador",
        IsActive = true,
        WalletId = Guid.Parse(AmbassadorWallet),
        MarketingCampaigns = new()
        {
            new IikoMarketingCampaign
            {
                Id = Guid.NewGuid(), Name = "Ambassador ödəniş", IsActive = true,
                OrderActionConditionBindings = new() { Bind(new[] { Rule("LoyaltyProgramEngine.Actions.Payment.PaymentAction", PimsPayment) }) },
                OverdraftActionConditionBindings = new()
                {
                    Bind(new[] { Rule("LoyaltyProgramEngine.Actions.Payment.OverdraftAction", Overdraft) },
                         new[] { Rule("LoyaltyProgramEngine.Conditions.GuestCategoryCondition", GuestCategory) })
                }
            }
        }
    };

    private static IikoLoyaltyProgramDetail CashbackProgram() => new()
    {
        Id = Guid.NewGuid(),
        Name = "Cashback",
        IsActive = true,
        WalletId = Guid.Parse(CashbackWallet),
        MarketingCampaigns = new()
        {
            new IikoMarketingCampaign
            {
                Id = Guid.NewGuid(), Name = "Пополнение", IsActive = true,
                OrderActionConditionBindings = new() { Bind(new[] { Rule("LoyaltyProgramEngine.Actions.RefillWalletPercentAction", Refill5) }) }
            }
        }
    };

    [Fact]
    public void Parser_ExtractsPaymentSpecAndGuestCategoryFromOverdraftBinding()
    {
        var c = IikoRuleParser.Summarize(PimsAmbassadorProgram()).Campaigns.Single();

        c.Kind.Should().Be(IikoRuleParser.KindPayment);
        c.Payment.Should().NotBeNull();
        c.Payment!.WalletId.Should().Be(AmbassadorWallet);
        c.Payment.UnitsPerItem.Should().Be(1);
        c.Payment.WhatToPay.Should().Be(1);
        c.Payment.Masks.Should().ContainSingle(m => m.Value == "Food And Beverage" && m.Type == 3);
        c.WalletIds.Should().Equal(AmbassadorWallet);
        // şərt overdraft binding-indədir (sifariş binding-ində deyil) — yenə tapılmalıdır
        c.GuestCategoryIds.Should().Equal(AmbassadorCategory);
        c.ExcludedGuestCategoryIds.Should().BeEmpty();
    }

    [Fact]
    public void Parser_RefillCampaign_WalletIdFromSettings_NoPayment()
    {
        var c = IikoRuleParser.Summarize(CashbackProgram()).Campaigns.Single();
        c.Kind.Should().Be(IikoRuleParser.KindCashbackPercent);
        c.Percent.Should().Be(5);
        c.WalletIds.Should().Equal(CashbackWallet);
        c.Payment.Should().BeNull();
        c.GuestCategoryIds.Should().BeEmpty();
    }

    [Fact]
    public void Merge_DedupesProgramsAcrossVenues_AndBuildsWallets()
    {
        var cashback = IikoRuleParser.Summarize(CashbackProgram());
        var ambassador = IikoRuleParser.Summarize(PimsAmbassadorProgram());

        var programs = IikoCatalogMerge.Programs(new[]
        {
            ("Port Baku", new List<IikoProgramSummary> { cashback, ambassador }),
            ("Gənclik", new List<IikoProgramSummary> { cashback }),
        });

        programs.Should().HaveCount(2);
        programs.Single(p => p.Name == "Cashback").Venues.Should().Equal("Port Baku", "Gənclik");
        programs.Single(p => p.Name == "Cashback").CashbackPercent.Should().Be(5);

        var wallets = IikoCatalogMerge.Wallets(programs);
        wallets.Should().HaveCount(2);
        var amb = wallets.Single(w => w.Id == AmbassadorWallet);
        amb.HasPayment.Should().BeTrue();
        amb.UnitsPerItem.Should().Be(1);
        amb.GuestCategoryIds.Should().Equal(AmbassadorCategory);
        var cb = wallets.Single(w => w.Id == CashbackWallet);
        cb.HasPayment.Should().BeFalse();
        cb.UsedBy.Should().ContainSingle(u => u.StartsWith("Пополнение"));
    }

    [Fact]
    public void Merge_GuestCategories_FetchedPlusReferencedUnknown()
    {
        var ambassador = IikoRuleParser.Summarize(PimsAmbassadorProgram());
        var programs = IikoCatalogMerge.Programs(new[] { ("Port Baku", new List<IikoProgramSummary> { ambassador }) });

        var fetched = new[]
        {
            new IikoGuestCategory { Id = Guid.Parse(AmbassadorCategory), Name = "Ambassador", IsActive = true },
            new IikoGuestCategory { Id = Guid.Parse(AmbassadorCategory), Name = "Ambassador", IsActive = true }, // ikinci org — təkrar
            new IikoGuestCategory { Id = Guid.NewGuid(), Name = "VIP", IsActive = true },
        };
        var cats = IikoCatalogMerge.GuestCategories(fetched, programs);

        cats.Should().HaveCount(2);
        var amb = cats.Single(c => c.Id == AmbassadorCategory);
        amb.Name.Should().Be("Ambassador");
        amb.UsedBy.Should().Equal("Ambassador ödəniş");

        // iiko siyahısı alınmasa da kampaniyada istinad olunan kateqoriya görünür (adsız)
        IikoCatalogMerge.GuestCategories(Array.Empty<IikoGuestCategory>(), programs)
            .Should().ContainSingle(c => c.Id == AmbassadorCategory && c.Name == null);
    }
}

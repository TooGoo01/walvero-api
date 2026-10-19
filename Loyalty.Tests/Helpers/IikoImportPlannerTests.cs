using FluentAssertions;
using Loyalty.Business.Helpers;
using Loyalty.Business.Services.Abstractions.Main;
using Loyalty.DataAccess.Enums;

namespace Loyalty.Tests.Helpers;

/// <summary>Kassa → Walvero idxal planı (pims: keşbek 5% + ambassador say ödənişi) və sinxron vəziyyəti.</summary>
public class IikoImportPlannerTests
{
    private const string CashbackWallet = "d0b10000-8bc3-62f6-adf1-08df1ee98316";
    private const string AmbassadorWallet = "7dd50000-64fa-c2b3-6e83-08df2c4fda0c";
    private const string AmbassadorCategory = "69a52f99-ea91-456c-89f5-d2effbc28159";
    private const string CashbackProgramId = "11111111-1111-1111-1111-111111111111";
    private const string AmbassadorProgramId = "22222222-2222-2222-2222-222222222222";

    private static IikoCampaignSummary Cashback5() => new()
    {
        Id = "c-cashback", Name = "Пополнение", IsActive = true, Kind = IikoRuleParser.KindCashbackPercent,
        KindLabel = "Keşbek %", Percent = 5, AllProducts = true, WalletIds = new() { CashbackWallet }
    };

    private static IikoCampaignSummary AmbassadorPayment() => new()
    {
        Id = "c-amb", Name = "Ambassador ödəniş", IsActive = true, Kind = IikoRuleParser.KindPayment, KindLabel = "Balansla ödəniş",
        WalletIds = new() { AmbassadorWallet },
        Payment = new IikoPaymentSpec(AmbassadorWallet, 1, 1, null, new() { new IikoProductMask("Food And Beverage", 3, null) }),
        GuestCategoryIds = new() { AmbassadorCategory }
    };

    private static List<IikoCatalogProgramDto> PimsCatalog() => new()
    {
        new() { Id = CashbackProgramId, Name = "Cashback", IsActive = true, WalletId = CashbackWallet, Campaigns = new() { Cashback5() } },
        new() { Id = AmbassadorProgramId, Name = "Ambassador", IsActive = true, WalletId = AmbassadorWallet, Campaigns = new() { AmbassadorPayment() } },
    };

    private static WalveroPosState PimsToday() => new()
    {
        TenantId = 56,
        AmbassadorsEnabled = true,
        IntegrationProgramId = 52,
        Programs = new()
        {
            new WalveroProgramState { Id = 52, Name = "Pims Loyalty", Audience = ProgramAudience.Public, TemplateType = CardTemplateType.PointsBased,
                PosEarnSource = PosEarnSource.Legacy, BronzePercent = 5, Currency = "AZN" }
        }
    };

    [Fact]
    public void Pims_ProposesCashbackMirrorAndNewAmbassadorProgram()
    {
        var proposals = IikoImportPlanner.Plan(PimsCatalog(), PimsToday());

        var cashback = proposals.Single(p => p.Kind == IikoImportPlanner.KindCashback);
        cashback.Applicable.Should().BeTrue();
        cashback.SelectedByDefault.Should().BeTrue();
        cashback.Changes.Should().Contain(c => c.ProgramId == 52 && c.Field == "PosEarnSource" && c.To == "PosMirror");
        cashback.Changes.Should().Contain(c => c.Field == "PosWalletId" && c.To == CashbackWallet);
        cashback.Changes.Should().Contain(c => c.Field == "PosExternalProgramId" && c.To == CashbackProgramId);
        cashback.Changes.Should().NotContain(c => c.Field == "BronzePercent"); // artıq 5%

        var amb = proposals.Single(p => p.Kind == IikoImportPlanner.KindUnitPayment);
        amb.Applicable.Should().BeTrue();
        amb.Warning.Should().BeNull();
        amb.Changes.Should().Contain(c => c.Target == IikoImportPlanner.TargetNewAmbassadorProgram && c.Field == "PosWalletId" && c.To == AmbassadorWallet);
        amb.Changes.Should().Contain(c => c.Target == IikoImportPlanner.TargetIntegration && c.To == AmbassadorCategory);
    }

    [Fact]
    public void AlreadyConfigured_IsIdempotent_NothingApplicable()
    {
        var state = PimsToday();
        state.AmbassadorCategoryId = AmbassadorCategory;
        state.Programs[0].PosEarnSource = PosEarnSource.PosMirror;
        state.Programs[0].PosWalletId = CashbackWallet;
        state.Programs[0].PosExternalProgramId = CashbackProgramId;
        state.Programs.Add(new WalveroProgramState
        {
            Id = 60, Name = "Ambassador", Audience = ProgramAudience.Ambassador, TemplateType = CardTemplateType.PointsBased,
            PosEarnSource = PosEarnSource.PosMirror, PosWalletId = AmbassadorWallet, PosExternalProgramId = AmbassadorProgramId, Currency = "məhsul"
        });

        var proposals = IikoImportPlanner.Plan(PimsCatalog(), state);
        proposals.Where(p => p.Kind is IikoImportPlanner.KindCashback or IikoImportPlanner.KindUnitPayment)
            .Should().OnlyContain(p => p.AlreadyApplied && !p.Applicable);
    }

    [Fact]
    public void ExistingStampAmbassadorProgram_WarnsAndNoCurrencyChange()
    {
        var state = PimsToday();
        state.Programs.Add(new WalveroProgramState { Id = 60, Name = "Ambassador", Audience = ProgramAudience.Ambassador, TemplateType = CardTemplateType.ProgressBased });
        var amb = IikoImportPlanner.Plan(PimsCatalog(), state).Single(p => p.Kind == IikoImportPlanner.KindUnitPayment);
        amb.Warning.Should().Contain("möhür kartıdır");
        amb.SelectedByDefault.Should().BeFalse();
        amb.Changes.Should().NotContain(c => c.Field == "Currency");
    }

    [Fact]
    public void AmbassadorsDisabled_WarnsButStillApplicable()
    {
        var state = PimsToday();
        state.AmbassadorsEnabled = false;
        var amb = IikoImportPlanner.Plan(PimsCatalog(), state).Single(p => p.Kind == IikoImportPlanner.KindUnitPayment);
        amb.Applicable.Should().BeTrue();
        amb.SelectedByDefault.Should().BeFalse();
        amb.Warning.Should().Contain("söndürülüb");
    }

    [Fact]
    public void ProductScopedAndUnsupported_AreInformational()
    {
        var catalog = new List<IikoCatalogProgramDto>
        {
            new()
            {
                Id = CashbackProgramId, Name = "Cashback", IsActive = true, WalletId = CashbackWallet,
                Campaigns = new()
                {
                    new() { Id = "c1", Name = "Şokolad 10%", IsActive = true, Kind = IikoRuleParser.KindCashbackPercent, KindLabel = "Keşbek %", Percent = 10, AllProducts = false },
                    new() { Id = "c2", Name = "Overdraft", IsActive = true, Kind = IikoRuleParser.KindOther, KindLabel = "Overdraft Action" },
                    new() { Id = "c3", Name = "Köhnə", IsActive = false, Kind = IikoRuleParser.KindCashbackPercent, AllProducts = true, Percent = 3 },
                }
            }
        };
        var proposals = IikoImportPlanner.Plan(catalog, PimsToday());
        proposals.Should().HaveCount(2); // passiv kampaniya keçilir
        proposals.Single(p => p.Key == "info:c1").Applicable.Should().BeFalse();
        proposals.Single(p => p.Key == "unsupported:c2").Kind.Should().Be(IikoImportPlanner.KindUnsupported);
    }

    [Fact]
    public void Status_FlagsLegacyDoubleCountAndUnlinkedWallets()
    {
        var catalog = new IikoCatalogDto
        {
            Programs = PimsCatalog(),
            Wallets = IikoCatalogMerge.Wallets(PimsCatalog()),
            GuestCategories = new() { new() { Id = AmbassadorCategory, Name = "Ambassador", IsActive = true } },
            Nomenclature = null
        };
        var items = IikoImportPlanner.Status(catalog, PimsToday());

        items.Should().Contain(i => i.Level == "warn" && i.Area == "program" && i.Text.Contains("hər ikisi"));
        items.Count(i => i.Area == "wallet" && i.Text.Contains("bağlanmayıb")).Should().Be(2);
        items.Should().Contain(i => i.Area == "nomenclature" && i.Level == "warn");
    }

    [Fact]
    public void Status_ProgramWalletMissingInCatalog_IsError()
    {
        var state = PimsToday();
        state.Programs[0].PosWalletId = "99999999-9999-9999-9999-999999999999";
        var catalog = new IikoCatalogDto { Programs = PimsCatalog(), Wallets = IikoCatalogMerge.Wallets(PimsCatalog()) };
        IikoImportPlanner.Status(catalog, state).Should().Contain(i => i.Level == "error" && i.Text.Contains("tapılmadı"));
    }
}

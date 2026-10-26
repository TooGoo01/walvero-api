using FluentAssertions;
using Loyalty.Business.Helpers;
using Loyalty.Business.Services.Abstractions.Main;
using Loyalty.DataAccess.Entities.Main;
using Loyalty.DataAccess.Enums;

namespace Loyalty.Tests.Helpers;

/// <summary>Kassa "N al → 1 pulsuz" → Walvero qazanc qaydaları + hədiyyə (Cupcup: desert 7, içki 10).</summary>
public class IikoGiftImportPlannerTests
{
    private const string DesertCategoryId = "3654748d-d44f-c57b-0193-fae4d3d1102d";
    private const string DessertDiscount = "4581b40b-2cdc-1642-019b-e5c3a66aebce";
    private const string DrinkDiscount = "3654748d-d44f-c57b-0194-238e01e4aa2a";

    private static List<IikoCatalogProgramDto> Catalog() => new[] { CupcupIikoFixture.DessertProgram(), CupcupIikoFixture.DrinkProgram() }
        .Select(p => IikoRuleParser.Summarize(p))
        .Select(s => new IikoCatalogProgramDto { Id = s.Id, Name = s.Name, IsActive = s.IsActive, WalletId = s.WalletId, Campaigns = s.Campaigns })
        .ToList();

    private static WalveroPosState CupcupToday() => new()
    {
        TenantId = 59,
        IntegrationProgramId = 56,
        Programs = new()
        {
            new WalveroProgramState { Id = 56, Name = "Cup-Cup Loyalty", Audience = ProgramAudience.Public,
                TemplateType = CardTemplateType.ProgressBased, PosEarnSource = PosEarnSource.Legacy, TotalSteps = 7 }
        },
        EarnRules = new() { new WalveroEarnRuleState(null, PosEarnExclusionKind.Category, DesertCategoryId, null) },
        ObservedDiscounts = new()
        {
            new IikoDiscountOptionDto(DrinkDiscount, "İçki hədiyyə", null),
            new IikoDiscountOptionDto(DessertDiscount, "Оплата со счета", null),
            new IikoDiscountOptionDto("9ecd1fd8-4166-4879-aba4-b4802cc2c7c0", "10% Termus Endirimi", null),
        },
        CategoryIdByName = new(StringComparer.OrdinalIgnoreCase) { ["DESERT"] = DesertCategoryId, ["HOT BAR"] = "hot-id" }
    };

    private static string DessertKey => $"{IikoImportPlanner.KindGiftReward}:{CupcupIikoFixture.DessertProgramId}:7";
    private static string DrinkKey => $"{IikoImportPlanner.KindGiftReward}:{CupcupIikoFixture.DrinkProgramId}:10";

    [Fact]
    public void Dessert_RewardProposal_DiscountMatchedByCampaignName()
    {
        var proposals = IikoImportPlanner.Plan(Catalog(), CupcupToday());
        var p = proposals.Single(x => x.Key == DessertKey);

        p.Kind.Should().Be(IikoImportPlanner.KindGiftReward);
        p.Applicable.Should().BeTrue();
        p.SelectedByDefault.Should().BeFalse();
        p.Warning.Should().BeNull(); // 7 möhür = 7 vahid, endirim tapıldı
        p.DiscountTypeSelectable.Should().BeTrue();
        p.DiscountTypeId.Should().Be(DessertDiscount);
        p.Changes.Should().Contain(c => c.Target == IikoImportPlanner.TargetReward && c.Field == "Name" && c.To == "Pulsuz 1 dessert" && c.ProgramId == 56);
        p.Changes.Should().Contain(c => c.Field == "PointsCost" && c.To == "7");
        p.Changes.Should().Contain(c => c.Field == "Products" && c.To == "01755,01753,02004");
        p.Changes.Should().Contain(c => c.Field == "PosDiscountTypeId" && c.To == DessertDiscount);
        p.Changes.Should().Contain(c => c.Field == "PosSourceKey" && c.To == $"{CupcupIikoFixture.DessertProgramId}:7");
    }

    [Fact]
    public void Drink_GiftCampaignsMergedByCost_NeedsDiscountChoice()
    {
        var proposals = IikoImportPlanner.Plan(Catalog(), CupcupToday());
        var drink = proposals.Where(x => x.Kind == IikoImportPlanner.KindGiftReward && x.Key.Contains(CupcupIikoFixture.DrinkProgramId.ToString())).ToList();

        drink.Should().ContainSingle(); // Bir/İki/Üç pulsuz içki → bir hədiyyə (10 vahid)
        var p = drink[0];
        p.Key.Should().Be(DrinkKey);
        p.DiscountTypeId.Should().BeNull(); // "İçki hədiyyə" kampaniya adı ilə uyğun gəlmir
        p.Warning.Should().Contain("Kassa endirimini seçin").And.Contain("7 möhürdə");
        p.Changes.Should().Contain(c => c.Field == "Name" && c.To == "Bir pulsuz içki");
        p.Changes.Should().Contain(c => c.Field == "Products" && c.To == "01707");
        p.Title.Should().Contain("istənilən ölçülü məhsul");
    }

    [Fact]
    public void DessertEarn_SkipsExistingCategory_AddsProductCodes()
    {
        var p = IikoImportPlanner.Plan(Catalog(), CupcupToday())
            .Single(x => x.Key == $"{IikoImportPlanner.KindGiftEarn}:{CupcupIikoFixture.DessertProgramId}");

        p.Changes.Should().NotContain(c => c.Field == "Category"); // DESERT artıq var
        p.Changes.Where(c => c.Field == "Product").Select(c => c.To).Should().Equal("02004", "02006");
        p.Changes.Should().OnlyContain(c => c.Target == IikoImportPlanner.TargetEarnRule && c.ProgramId == 56 && c.Amount == 1);
        p.Warning.Should().BeNull();
        p.SelectedByDefault.Should().BeFalse();
    }

    [Fact]
    public void DrinkEarn_UnknownCategory_Warns()
    {
        var p = IikoImportPlanner.Plan(Catalog(), CupcupToday())
            .Single(x => x.Key == $"{IikoImportPlanner.KindGiftEarn}:{CupcupIikoFixture.DrinkProgramId}");

        p.Changes.Should().Contain(c => c.Field == "Category" && c.To == "hot-id" && c.TargetName == "HOT BAR");
        p.Warning.Should().Contain("COLD BAR");
    }

    [Fact]
    public void GiftAndEarnCampaigns_NotListedAsUnsupported()
    {
        var proposals = IikoImportPlanner.Plan(Catalog(), CupcupToday());
        proposals.Should().NotContain(p => p.Kind == IikoImportPlanner.KindUnsupported);
    }

    [Fact]
    public void ExistingReward_IsIdempotent()
    {
        var state = CupcupToday();
        state.Rewards.Add(new WalveroRewardState
        {
            Id = 900, ProgramId = 56, Name = "Pulsuz desert", PointsCost = 7,
            PosSourceKey = $"{CupcupIikoFixture.DessertProgramId}:7", PosDiscountTypeId = DessertDiscount,
            ProductCodes = new() { "01755", "01753", "02004" }
        });

        var p = IikoImportPlanner.Plan(Catalog(), state).Single(x => x.Key == DessertKey);
        p.AlreadyApplied.Should().BeTrue();
        p.Applicable.Should().BeFalse();
        p.Changes.Should().BeEmpty();
    }

    [Fact]
    public void ExistingReward_NewProductCodes_UpdateWithRewardId()
    {
        var state = CupcupToday();
        state.Rewards.Add(new WalveroRewardState
        {
            Id = 900, ProgramId = 56, Name = "Pulsuz desert", PointsCost = 7,
            PosSourceKey = $"{CupcupIikoFixture.DessertProgramId}:7", PosDiscountTypeId = DessertDiscount,
            ProductCodes = new() { "01755" }
        });

        var p = IikoImportPlanner.Plan(Catalog(), state).Single(x => x.Key == DessertKey);
        p.Applicable.Should().BeTrue();
        p.Changes[0].Field.Should().Be("RewardId");
        p.Changes[0].To.Should().Be("900");
        p.Changes.Should().Contain(c => c.Field == "Products" && c.To == "01753,02004");
        p.Changes.Should().NotContain(c => c.Field == "Name");
    }

    [Fact]
    public void ExistingReward_WithoutDiscount_StaysApplicableForDiscountChoice()
    {
        var state = CupcupToday();
        state.Rewards.Add(new WalveroRewardState
        {
            Id = 901, ProgramId = 56, Name = "Bir pulsuz içki", PointsCost = 10,
            PosSourceKey = $"{CupcupIikoFixture.DrinkProgramId}:10", ProductCodes = new() { "01707" }
        });

        var p = IikoImportPlanner.Plan(Catalog(), state).Single(x => x.Key == DrinkKey);
        p.Applicable.Should().BeTrue();
        p.AlreadyApplied.Should().BeFalse();
        p.Changes.Should().ContainSingle(c => c.Field == "RewardId" && c.To == "901");
    }

    [Fact]
    public void SyncStatus_ShowsGiftLinkState()
    {
        var state = CupcupToday();
        state.Rewards.Add(new WalveroRewardState
        {
            Id = 900, ProgramId = 56, Name = "Pulsuz 1 dessert", PointsCost = 7,
            PosSourceKey = $"{CupcupIikoFixture.DessertProgramId}:7", PosDiscountTypeId = DessertDiscount
        });
        var catalog = new IikoCatalogDto { Programs = Catalog() };

        var items = IikoImportPlanner.Status(catalog, state).Where(i => i.Area == "reward").ToList();
        items.Should().Contain(i => i.Level == "ok" && i.Text.Contains("Pulsuz 1 dessert"));
        items.Should().Contain(i => i.Level == "info" && i.Text.Contains("10 vahid"));
    }
}

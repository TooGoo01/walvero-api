using FluentAssertions;
using Loyalty.Business.Helpers;

namespace Loyalty.Tests.Helpers;

/// <summary>Menyu popup-unun filial hədəfi: null = bütün filiallar; ümumi menyu (filialsız) yalnız bütün filiallara aid popup-u görür.</summary>
public class MenuPopupTargetingTests
{
    [Fact]
    public void AllBranches_ShownEverywhere()
    {
        MenuPopupTargeting.AppliesTo(null, null).Should().BeTrue();
        MenuPopupTargeting.AppliesTo(null, 5).Should().BeTrue();
        MenuPopupTargeting.AppliesTo("[]", 5).Should().BeTrue();
    }

    [Fact]
    public void SelectedBranches_OnlyThoseBranches_NotGeneralMenu()
    {
        var json = MenuPopupTargeting.Serialize(new[] { 3, 5 });
        MenuPopupTargeting.AppliesTo(json, 5).Should().BeTrue();
        MenuPopupTargeting.AppliesTo(json, 4).Should().BeFalse();
        MenuPopupTargeting.AppliesTo(json, null).Should().BeFalse();   // /menu (filialsız)
    }

    [Fact]
    public void Serialize_EmptyIsNull_DedupesAndSorts()
    {
        MenuPopupTargeting.Serialize(null).Should().BeNull();
        MenuPopupTargeting.Serialize(new[] { 0, -1 }).Should().BeNull();
        MenuPopupTargeting.Serialize(new[] { 7, 3, 7 }).Should().Be("[3,7]");
    }

    [Fact]
    public void Parse_InvalidJson_TreatedAsAllBranches()
    {
        MenuPopupTargeting.Parse("not json").Should().BeEmpty();
        MenuPopupTargeting.AppliesTo("not json", null).Should().BeTrue();
    }
}

using FluentAssertions;
using Loyalty.Business.Dtos.Get.Main.Branch;
using Loyalty.Business.Helpers;

namespace Loyalty.Tests.Helpers;

/// <summary>Filial yaxınlıq bildirişi cütləri — normalizasiya, JSON round-trip, təsadüfi seçim.</summary>
public class BranchGeoTextsTests
{
    private static BranchGeoTextDto Item(string? title, string text) => new() { Title = title, Text = text };

    [Fact]
    public void Serialize_TrimsDropsEmptyTextAndDuplicates()
    {
        var json = BranchGeoTexts.Serialize(new[]
        {
            Item("  Salam ", "  Kofen hazırdır  "),
            Item("Başlıq", "   "),                 // boş mətn → atılır
            Item("Salam", "Kofen hazırdır"),       // trim-dən sonra dublikat
            Item("", "Yalnız mətn"),               // boş başlıq → null
        });

        var list = BranchGeoTexts.Parse(json);
        list.Should().HaveCount(2);
        list[0].Title.Should().Be("Salam");
        list[0].Text.Should().Be("Kofen hazırdır");
        list[1].Title.Should().BeNull();
        list[1].Text.Should().Be("Yalnız mətn");
    }

    [Fact]
    public void Serialize_CutsLengthsAndLimitsCount()
    {
        var items = Enumerable.Range(1, 15)
            .Select(i => Item(i + new string('T', 60), i + new string('x', 150)));

        var list = BranchGeoTexts.Parse(BranchGeoTexts.Serialize(items));

        list.Should().HaveCount(BranchGeoTexts.MaxItems);
        list.Should().OnlyContain(x => x.Title!.Length <= BranchGeoTexts.MaxTitleLength
                                       && x.Text.Length <= BranchGeoTexts.MaxTextLength);
    }

    [Fact]
    public void Serialize_EmptyOrNull_ReturnsNull()
    {
        BranchGeoTexts.Serialize(null).Should().BeNull();
        BranchGeoTexts.Serialize(Array.Empty<BranchGeoTextDto>()).Should().BeNull();
        BranchGeoTexts.Serialize(new[] { Item("Başlıq", "") }).Should().BeNull();
    }

    [Fact]
    public void Parse_InvalidJson_ReturnsEmpty()
    {
        BranchGeoTexts.Parse(null).Should().BeEmpty();
        BranchGeoTexts.Parse("not json").Should().BeEmpty();
        BranchGeoTexts.Parse("{\"title\":\"x\"}").Should().BeEmpty();
    }

    [Fact]
    public void Pick_ReturnsItemFromList_OrNull()
    {
        BranchGeoTexts.Pick(null).Should().BeNull();

        var json = BranchGeoTexts.Serialize(new[] { Item("A", "a"), Item("B", "b") });
        for (var i = 0; i < 20; i++)
            BranchGeoTexts.Pick(json)!.Text.Should().BeOneOf("a", "b");

        BranchGeoTexts.Pick(BranchGeoTexts.Serialize(new[] { Item(null, "tək") }))!.Text.Should().Be("tək");
    }
}

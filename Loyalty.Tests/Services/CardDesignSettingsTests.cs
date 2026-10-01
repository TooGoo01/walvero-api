using FluentAssertions;
using Loyalty.Business.Dtos.Post.Main;

namespace Loyalty.Tests.Services;

public class CardDesignSettingsTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("{not json")]
    [InlineData("[1,2]")]
    public void Parse_EmptyOrInvalid_ReturnsDefaults(string? json)
    {
        var d = CardDesignSettings.Parse(json);
        d.Title.Should().BeNull();
        d.EffectiveBalanceLabel.Should().Be(CardDesignSettings.DefaultBalanceLabel);
        d.EffectiveAboutTitle.Should().Be(CardDesignSettings.DefaultAboutTitle);
        d.ShowAbout.Should().BeTrue();
        d.ShowWebsite.Should().BeTrue();
    }

    [Fact]
    public void ToJson_Parse_RoundTrips_AndOmitsNulls()
    {
        var d = new CardDesignSettings { Title = "Pubzade", BalanceLabel = "Balans", WebsiteUrl = "-" };
        var json = d.ToJson();
        json.Should().Contain("\"title\":\"Pubzade\"").And.NotContain("logoText");
        var back = CardDesignSettings.Parse(json);
        back.Title.Should().Be("Pubzade");
        back.EffectiveBalanceLabel.Should().Be("Balans");
        back.ShowWebsite.Should().BeFalse();
        back.ShowAbout.Should().BeTrue();
        back.Version.Should().Be(CardDesignSettings.CurrentVersion);
    }

    [Fact]
    public void Apply_NullKeeps_EmptyClears_ValueSets()
    {
        var d = new CardDesignSettings { Title = "Old", LogoText = "LT", AboutText = "About" };
        d.Apply(title: null, logoText: "", description: " New desc ", issuerName: null,
            balanceLabel: null, customerLabel: null, cashbackLabel: null,
            aboutTitle: null, aboutText: "-", websiteUrl: null, websiteLabel: null);
        d.Title.Should().Be("Old");            // null → toxunulmur
        d.LogoText.Should().BeNull();          // "" → silinir (default-a qayıt)
        d.Description.Should().Be("New desc"); // trim
        d.ShowAbout.Should().BeFalse();        // "-" → gizli
        d.EffectiveDescription.Should().Be("New desc");
    }
}

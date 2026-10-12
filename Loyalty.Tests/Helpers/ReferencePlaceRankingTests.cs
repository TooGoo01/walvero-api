using FluentAssertions;
using Loyalty.Business.Helpers;
using Loyalty.DataAccess.Entities.Main;

namespace Loyalty.Tests.Helpers;

/// <summary>AI xarici məkan axtarışı: açar söz skoru + yaxınlıq (radius) filtri və məsafə sırası.</summary>
public class ReferencePlaceRankingTests
{
    // Bakı: Fəvvarələr meydanı ~ (40.3710, 49.8370); İçərişəhər ~ (40.3665, 49.8363); Port Baku ~ (40.3768, 49.8632)
    private const double FountainLat = 40.3710, FountainLng = 49.8370;

    private static AiReferencePlace P(string name, string? keywords = null, decimal? lat = null, decimal? lng = null, string? area = null)
        => new() { Name = name, Keywords = keywords, Latitude = lat, Longitude = lng, Area = area, IsActive = true };

    private static readonly List<AiReferencePlace> Places = new()
    {
        P("Qala Divari", "milli mətbəx, kabab, terras", 40.36853m, 49.83556m, "İçərişəhər"),
        P("Marani Restaurant", "gürcü mətbəxi, şərab", 40.37259m, 49.84595m),
        P("The Hunger Port Baku", "burger, kokteyl", 40.37675m, 49.86320m, "Port Baku"),
        P("Old Gürcü Place", "gürcü mətbəxi"), // koordinatsız
        P("Dərya Fish House", "balıq", 40.30389m, 49.82799m),
    };

    [Fact]
    public void HaversineKm_KnownDistance()
    {
        // Fəvvarələr meydanı → Port Baku ≈ 2.3 km
        var d = GeoMath.HaversineKm(FountainLat, FountainLng, 40.3768, 49.8632);
        d.Should().BeInRange(2.1, 2.5);
        GeoMath.HaversineKm(FountainLat, FountainLng, FountainLat, FountainLng).Should().Be(0);
    }

    [Fact]
    public void KeywordsOnly_DiacriticInsensitive_NoGeo()
    {
        var hits = ReferencePlaceRanking.Rank(Places, new[] { "gurcu" }, null, null, null, 8);
        hits.Select(h => h.Place.Name).Should().BeEquivalentTo(new[] { "Marani Restaurant", "Old Gürcü Place" });
        hits.Should().OnlyContain(h => h.DistanceKm == null);
    }

    [Fact]
    public void KeywordsWithOrigin_NoRadius_KeepsAll_AddsDistance()
    {
        var hits = ReferencePlaceRanking.Rank(Places, new[] { "gürcü" }, FountainLat, FountainLng, null, 8);
        hits.Should().HaveCount(2);
        hits[0].Place.Name.Should().Be("Marani Restaurant"); // məsafəsi olan öndə (eyni skor)
        hits[0].DistanceKm.Should().NotBeNull();
        hits[1].DistanceKm.Should().BeNull();
    }

    [Fact]
    public void Radius_DropsFarAndCoordinateless_SortsByDistance()
    {
        var hits = ReferencePlaceRanking.Rank(Places, new[] { "gürcü", "milli", "burger", "balıq" }, FountainLat, FountainLng, 5, 8);
        hits.Select(h => h.Place.Name).Should().NotContain(new[] { "Old Gürcü Place", "Dərya Fish House" });
        hits.Select(h => h.Place.Name).Should().Contain(new[] { "Qala Divari", "Marani Restaurant", "The Hunger Port Baku" });
        hits.Should().BeInAscendingOrder(h => h.DistanceKm); // eyni skor (1) → məsafə ilə
    }

    [Fact]
    public void Radius_WithoutKeywords_ReturnsNearest()
    {
        var hits = ReferencePlaceRanking.Rank(Places, Array.Empty<string>(), FountainLat, FountainLng, 1.5, 8);
        hits.Select(h => h.Place.Name).Should().Equal("Qala Divari", "Marani Restaurant");
    }

    [Fact]
    public void HigherScoreBeatsDistance_AndAreaTokenMatches()
    {
        var hits = ReferencePlaceRanking.Rank(Places, new[] { "milli", "İçərişəhər" }, FountainLat, FountainLng, 10, 8);
        hits[0].Place.Name.Should().Be("Qala Divari");
        hits[0].Score.Should().Be(2);
    }

    [Fact]
    public void NoTokens_NoGeo_Empty_And_TakeClamped()
    {
        ReferencePlaceRanking.Rank(Places, Array.Empty<string>(), null, null, null, 8).Should().BeEmpty();
        ReferencePlaceRanking.Rank(Places, new[] { "a" }, null, null, null, 1).Should().HaveCount(1);
    }
}

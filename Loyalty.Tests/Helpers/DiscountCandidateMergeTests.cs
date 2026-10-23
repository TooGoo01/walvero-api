using FluentAssertions;
using Loyalty.Business.Helpers;

namespace Loyalty.Tests.Helpers;

/// <summary>Hədiyyə endirimi seçimi — iiko endirim siyahısı + sifarişlərdə görünən endirimlər.</summary>
public class DiscountCandidateMergeTests
{
    private static readonly DateTime T0 = new(2026, 10, 23, 10, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void MergesById_CatalogNameWins_ObservedOnlyKept()
    {
        var r = DiscountCandidateMerge.Merge(
            new[] { new DiscountCandidateSource("A", "Əməkdaş 50%", 50, false, true) },
            new[]
            {
                new ObservedDiscountSource("a", "old name", T0),
                new ObservedDiscountSource("B", "Sifarişdən", T0.AddHours(-1)),
            },
            Array.Empty<string>());

        r.Should().HaveCount(2);
        var a = r.Single(x => x.Id.Equals("A", StringComparison.OrdinalIgnoreCase));
        a.Name.Should().Be("Əməkdaş 50%");
        a.InCatalog.Should().BeTrue();
        a.LastSeenAtUtc.Should().Be(T0);
        r.Single(x => x.Id == "B").InCatalog.Should().BeFalse();
    }

    [Fact]
    public void WalveroName_SuggestedFirst_UnlessAlreadyLinked()
    {
        var r = DiscountCandidateMerge.Merge(
            new[]
            {
                new DiscountCandidateSource("X", "Endirim 10", 10, false, true),
                new DiscountCandidateSource("W", "Walvero hədiyyə", 100, false, true),
                new DiscountCandidateSource("L", "WALVERO köhnə", 100, false, true),
            },
            Array.Empty<ObservedDiscountSource>(),
            new[] { "L" });

        r[0].Id.Should().Be("W");
        r[0].Suggested.Should().BeTrue();
        r.Single(x => x.Id == "L").Suggested.Should().BeFalse();
        r.Single(x => x.Id == "L").Linked.Should().BeTrue();
        r[^1].Id.Should().Be("L"); // bağlı olanlar sonda
    }

    [Fact]
    public void DeletedCatalogDiscount_DroppedUnlessSeenInOrders()
    {
        var r = DiscountCandidateMerge.Merge(
            new[]
            {
                new DiscountCandidateSource("D1", "Silinmiş", 5, true, false),
                new DiscountCandidateSource("D2", "Silinmiş amma işlənib", 5, true, false),
            },
            new[] { new ObservedDiscountSource("D2", null, T0) },
            Array.Empty<string>());

        r.Should().ContainSingle(x => x.Id == "D2");
        r.Single().InCatalog.Should().BeFalse();
        r.Single().Name.Should().Be("Silinmiş amma işlənib");
    }
}

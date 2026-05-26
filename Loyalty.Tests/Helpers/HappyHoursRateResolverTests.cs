using FluentAssertions;
using Loyalty.Business.Dtos.Post.Main;
using Loyalty.Business.Helpers;

namespace Loyalty.Tests.Helpers;

public class HappyHoursRateResolverTests
{
    private static HappyHoursWindowDto Window(
        byte? dayOfWeek = null,
        DateOnly? specificDate = null,
        int startHour = 0,
        int endHour = 24,
        int cash = 10,
        int card = 5,
        string? label = null) =>
        new()
        {
            DayOfWeek = dayOfWeek,
            SpecificDate = specificDate,
            StartTime = new TimeOnly(startHour, 0),
            EndTime = endHour == 24 ? new TimeOnly(23, 59, 59) : new TimeOnly(endHour, 0),
            PercentCash = cash,
            PercentCard = card,
            Label = label
        };

    [Fact]
    public void Resolve_DayOfWeekAndHourMatch_ReturnsWindowPercents()
    {
        var monday2pm = new DateTime(2026, 6, 1, 14, 0, 0, DateTimeKind.Unspecified); // Mon
        var windows = new[] { Window(dayOfWeek: 1, startHour: 8, endHour: 20, cash: 20, card: 20) };

        var (cash, card) = HappyHoursRateResolver.Resolve(windows, basePercentCash: 5, basePercentCard: 5, monday2pm);

        cash.Should().Be(20);
        card.Should().Be(20);
    }

    [Fact]
    public void Resolve_OutsideWindow_FallsBackToBasePercents()
    {
        var monday10pm = new DateTime(2026, 6, 1, 22, 0, 0, DateTimeKind.Unspecified);
        var windows = new[] { Window(dayOfWeek: 1, startHour: 8, endHour: 20, cash: 20, card: 20) };

        var (cash, card) = HappyHoursRateResolver.Resolve(windows, basePercentCash: 10, basePercentCard: 5, monday10pm);

        cash.Should().Be(10);
        card.Should().Be(5);
    }

    [Fact]
    public void Resolve_OutsideWindow_NullBase_ReturnsNull()
    {
        var monday10pm = new DateTime(2026, 6, 1, 22, 0, 0, DateTimeKind.Unspecified);
        var windows = new[] { Window(dayOfWeek: 1, startHour: 8, endHour: 20) };

        var (cash, card) = HappyHoursRateResolver.Resolve(windows, basePercentCash: null, basePercentCard: null, monday10pm);

        cash.Should().BeNull();
        card.Should().BeNull();
    }

    [Fact]
    public void Resolve_SpecificDate_TakesPriorityOverDayOfWeek()
    {
        var monday2pm = new DateTime(2026, 6, 1, 14, 0, 0, DateTimeKind.Unspecified);
        var windows = new[]
        {
            Window(dayOfWeek: 1, startHour: 8, endHour: 20, cash: 10, card: 10, label: "weekday"),
            Window(specificDate: new DateOnly(2026, 6, 1), startHour: 8, endHour: 20, cash: 50, card: 50, label: "holiday")
        };

        var (cash, card) = HappyHoursRateResolver.Resolve(windows, basePercentCash: 5, basePercentCard: 5, monday2pm);

        cash.Should().Be(50);
        card.Should().Be(50);
    }

    [Fact]
    public void Resolve_NoWindowsNoBase_ReturnsNullPair()
    {
        var now = new DateTime(2026, 6, 1, 14, 0, 0, DateTimeKind.Unspecified);

        var (cash, card) = HappyHoursRateResolver.Resolve(
            windows: null, basePercentCash: null, basePercentCard: null, now);

        cash.Should().BeNull();
        card.Should().BeNull();
    }

    [Fact]
    public void Resolve_WindowEndExclusive()
    {
        var atExactEnd = new DateTime(2026, 6, 1, 20, 0, 0, DateTimeKind.Unspecified);
        var windows = new[] { Window(dayOfWeek: 1, startHour: 8, endHour: 20, cash: 20, card: 20) };

        var (cash, card) = HappyHoursRateResolver.Resolve(windows, basePercentCash: 5, basePercentCard: 5, atExactEnd);

        cash.Should().Be(5);
        card.Should().Be(5);
    }
}

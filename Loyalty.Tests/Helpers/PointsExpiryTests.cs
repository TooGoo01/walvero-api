using FluentAssertions;
using Loyalty.Business.Helpers;

namespace Loyalty.Tests.Helpers;

public class PointsExpiryTests
{
    private static readonly DateTime Now = new(2026, 9, 30, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void IsExpired_Null_NeverExpires()
        => PointsExpiry.IsExpired(null, Now).Should().BeFalse();

    [Fact]
    public void IsExpired_FutureDate_NotExpired()
        => PointsExpiry.IsExpired(Now.AddMinutes(1), Now).Should().BeFalse();

    [Fact]
    public void IsExpired_PastOrNow_Expired()
    {
        PointsExpiry.IsExpired(Now, Now).Should().BeTrue();
        PointsExpiry.IsExpired(Now.AddDays(-1), Now).Should().BeTrue();
    }

    [Fact]
    public void ResolveOnCredit_RequestedDate_ReplacesExisting()
    {
        var requested = Now.AddDays(10);
        PointsExpiry.ResolveOnCredit(Now.AddDays(3), requested, Now).Should().Be(requested);
        PointsExpiry.ResolveOnCredit(null, requested, Now).Should().Be(requested);
    }

    [Fact]
    public void ResolveOnCredit_NoRequest_PastDateCleared()
        => PointsExpiry.ResolveOnCredit(Now.AddDays(-1), null, Now).Should().BeNull();

    [Fact]
    public void ResolveOnCredit_NoRequest_FutureDateKept()
    {
        var existing = Now.AddDays(5);
        PointsExpiry.ResolveOnCredit(existing, null, Now).Should().Be(existing);
    }

    [Fact]
    public void ResolveOnCredit_NoRequest_NullStaysNull()
        => PointsExpiry.ResolveOnCredit(null, null, Now).Should().BeNull();
}

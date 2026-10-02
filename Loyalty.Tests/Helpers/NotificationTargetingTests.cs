using FluentAssertions;
using Loyalty.Business.Helpers;

namespace Loyalty.Tests.Helpers;

/// <summary>Bildirişlər v2 — pure qaydalar: DOB keyfiyyəti, yaş kəsimləri, ad günü (29 fevral), şablon, bonus, vaxt zonası, filtr.</summary>
public class NotificationTargetingTests
{
    private static readonly DateTime Today = new(2026, 10, 2);

    [Theory]
    [InlineData("1995-05-10", "2026-01-01", true)]
    [InlineData("2026-09-19", "2026-09-19", false)]   // picker default = qeydiyyat günü
    [InlineData("2023-01-01", "2026-01-01", false)]   // yaş < 6
    [InlineData("2020-10-02", "2026-01-01", true)]    // tam 6 yaş
    [InlineData("2020-10-03", "2026-01-01", false)]   // 6-ya 1 gün qalıb
    [InlineData("1990-03-15", "1990-03-15", false)]   // DOB = qeydiyyat günü (köhnə qeyd belə)
    public void BirthDate_IsUsable(string dob, string reg, bool expected)
        => BirthDateRules.IsUsable(DateTime.Parse(dob), DateTime.Parse(reg), Today).Should().Be(expected);

    [Fact]
    public void BirthDate_Null_NotUsable() => BirthDateRules.IsUsable(null, Today, Today).Should().BeFalse();

    [Theory]
    [InlineData("2008-10-02", 18)]
    [InlineData("2008-10-03", 17)]
    [InlineData("2000-02-29", 26)]
    public void Age_Computed(string dob, int age) => BirthDateRules.Age(DateTime.Parse(dob), Today).Should().Be(age);

    [Fact]
    public void AgeCutoffs_MatchAge()
    {
        // 18–25 yaş: DOB ≤ today-18y və DOB > today-26y
        var maxDob = BirthDateRules.MaxDobForMinAge(Today, 18);
        var minExcl = BirthDateRules.MinDobExclusiveForMaxAge(Today, 25);
        foreach (var dob in new[] { "2008-10-02", "2008-10-03", "2000-10-03", "2000-10-02", "2001-01-01" })
        {
            var d = DateTime.Parse(dob);
            var inRange = d <= maxDob && d > minExcl;
            var age = BirthDateRules.Age(d, Today);
            inRange.Should().Be(age is >= 18 and <= 25, dob);
        }
    }

    [Fact]
    public void Birthday_SameMonthDay()
    {
        BirthDateRules.IsBirthday(new DateTime(1990, 10, 2), Today).Should().BeTrue();
        BirthDateRules.IsBirthday(new DateTime(1990, 10, 3), Today).Should().BeFalse();
    }

    [Fact]
    public void Birthday_Feb29_OnFeb28_InNonLeapYear()
    {
        var feb29 = new DateTime(2000, 2, 29);
        BirthDateRules.IsBirthday(feb29, new DateTime(2027, 2, 28)).Should().BeTrue();   // 2027 uzun il deyil
        BirthDateRules.IsBirthday(feb29, new DateTime(2028, 2, 28)).Should().BeFalse();  // 2028 uzun ildir → 29-da
        BirthDateRules.IsBirthday(feb29, new DateTime(2028, 2, 29)).Should().BeTrue();
        BirthDateRules.BirthdayKeys(new DateTime(2027, 2, 28)).Should().HaveCount(2);
        BirthDateRules.BirthdayKeys(new DateTime(2028, 2, 28)).Should().HaveCount(1);
    }

    [Fact]
    public void NextBirthday_ThisYearOrNext()
    {
        BirthDateRules.NextBirthday(new DateTime(1990, 10, 5), Today).Should().Be(new DateTime(2026, 10, 5));
        BirthDateRules.NextBirthday(new DateTime(1990, 10, 2), Today).Should().Be(Today);
        BirthDateRules.NextBirthday(new DateTime(1990, 1, 5), Today).Should().Be(new DateTime(2027, 1, 5));
        BirthDateRules.NextBirthday(new DateTime(2000, 2, 29), Today).Should().Be(new DateTime(2027, 2, 28));
    }

    [Fact]
    public void Template_RendersPlaceholders()
    {
        NotificationTemplate.Render("Ad gününüz mübarək, {ad}!", "Leyla", "Pims Tea", null).Should().Be("Ad gününüz mübarək, Leyla!");
        NotificationTemplate.Render("{restoran}: sizə {hədiyyə} hədiyyə", "L", "Pims", "5 ₼").Should().Be("Pims: sizə 5 ₼ hədiyyə");
        NotificationTemplate.Render("Salam {ad}, xoş gəldiniz", null, null, null).Should().Be("Salam, xoş gəldiniz");
        NotificationTemplate.Render("{HEDIYYE}", null, null, "2 möhür").Should().Be("2 möhür");
    }

    [Theory]
    [InlineData(5, false, "5 ₼")]
    [InlineData(2.5, false, "2,5 ₼")]
    [InlineData(2, true, "2 möhür")]
    public void GiftText(decimal v, bool stamp, string expected) => NotificationTemplate.GiftText(v, stamp).Should().Be(expected);

    [Theory]
    [InlineData(5, 0, 12, 5)]     // heç nə xərclənməyib
    [InlineData(5, 3, 12, 2)]     // bonus əvvəl xərclənir
    [InlineData(5, 7, 12, 0)]     // bonus tam xərclənib
    [InlineData(5, 0, 3, 3)]      // balans bonusdan azdır (başqa yolla azalıb)
    [InlineData(5, -2, 12, 5)]    // mənfi "xərc" sayılmır
    public void BonusExpiry_Unspent(decimal amount, decimal spent, decimal current, decimal expected)
        => BonusExpiryMath.Unspent(amount, spent, current).Should().Be(expected);

    [Fact]
    public void TenantClock_BakuLocalToUtc_AndMonthRange()
    {
        var tz = TenantClock.Resolve("Azerbaijan Standard Time", null);
        TenantClock.ToUtc(new DateTime(2026, 10, 8, 10, 0, 0), tz).Should().Be(new DateTime(2026, 10, 8, 6, 0, 0));
        TenantClock.ToLocal(new DateTime(2026, 10, 8, 6, 0, 0, DateTimeKind.Utc), tz).Should().Be(new DateTime(2026, 10, 8, 10, 0, 0));
        var (start, end) = TenantClock.MonthRangeUtc(tz, new DateTime(2026, 10, 2, 12, 0, 0, DateTimeKind.Utc));
        start.Should().Be(new DateTime(2026, 9, 30, 20, 0, 0));   // 1 okt 00:00 Bakı
        end.Should().Be(new DateTime(2026, 10, 31, 20, 0, 0));
        // 31 okt 21:00 UTC = 1 noy 01:00 Bakı → noyabr
        var (s2, _) = TenantClock.MonthRangeUtc(tz, new DateTime(2026, 10, 31, 21, 0, 0, DateTimeKind.Utc));
        s2.Should().Be(new DateTime(2026, 10, 31, 20, 0, 0));
    }

    [Fact]
    public void TenantClock_FallsBackToReceiptThenDefault()
    {
        TenantClock.Resolve(null, null).Id.Should().Be(TenantClock.DefaultTimeZoneId);
        TenantClock.Resolve("Not/AZone", "Azerbaijan Standard Time").Id.Should().Be("Azerbaijan Standard Time");
    }

    [Fact]
    public void AudienceFilter_Validate()
    {
        new AudienceFilterDto().Validate().Should().BeNull();
        new AudienceFilterDto { AgeMin = 30, AgeMax = 20 }.Validate().Should().NotBeNull();
        new AudienceFilterDto { Genders = new() { 5 } }.Validate().Should().NotBeNull();
        new AudienceFilterDto { ActiveWithinDays = 30, InactiveForDays = 30 }.Validate().Should().NotBeNull();
        new AudienceFilterDto { MinBalance = 10, MaxBalance = 5 }.Validate().Should().NotBeNull();
        new AudienceFilterDto { ActiveWithinDays = 0 }.Validate().Should().NotBeNull();
    }

    [Fact]
    public void AudienceFilter_JsonRoundTrip_AndSummary()
    {
        var f = new AudienceFilterDto { Genders = new() { 1 }, AgeMin = 18, AgeMax = 25, InactiveForDays = 30, Channel = "wallet", IncludeUnknown = true };
        var back = AudienceFilterDto.Parse(f.ToJson());
        back.Genders.Should().Equal(1);
        back.AgeMin.Should().Be(18);
        back.EffectiveChannel.Should().Be("wallet");
        back.Summary().Should().Be("Qadın · 18–25 yaş · 30+ gündür gəlməyənlər · yalnız wallet · bilinməyənlər daxil");
        new AudienceFilterDto().Summary().Should().Be("Bütün müştərilər");
        AudienceFilterDto.Parse("{bad").Summary().Should().Be("Bütün müştərilər");
        AudienceFilterDto.Parse("{\"channel\":\"xyz\"}").EffectiveChannel.Should().Be("all");
    }
}

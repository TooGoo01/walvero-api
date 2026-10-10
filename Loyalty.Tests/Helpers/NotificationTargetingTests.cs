using FluentAssertions;
using Loyalty.Business.Helpers;
using Loyalty.DataAccess.Enums;

namespace Loyalty.Tests.Helpers;

/// <summary>Bildirişlər v2 — pure qaydalar: DOB keyfiyyəti, yaş kəsimləri, ad günü (29 fevral), şablon, bonus, vaxt zonası, filtr.</summary>
public class NotificationTargetingTests
{
    private static readonly DateTime Today = new(2026, 10, 2);

    [Theory]
    [InlineData("1995-05-10", "2026-01-01", true)]
    [InlineData("2026-09-19", "2026-09-19", false)]   // picker default = qeydiyyat günü
    [InlineData("2023-01-01", "2026-01-01", false)]   // yaş < 7
    [InlineData("2019-10-02", "2026-01-01", true)]    // tam 7 yaş
    [InlineData("2019-10-03", "2026-01-01", false)]   // 7-yə 1 gün qalıb
    [InlineData("1990-03-15", "1990-03-15", false)]   // DOB = qeydiyyat günü (köhnə qeyd belə)
    public void BirthDate_IsUsable(string dob, string reg, bool expected)
        => BirthDateRules.IsUsable(DateTime.Parse(dob), DateTime.Parse(reg), Today).Should().Be(expected);

    // Giriş validasiyası (web/app/import): boş ola bilər; 1900-01-01 ≤ DOB ≤ bu gün − 7 il
    [Theory]
    [InlineData(null, true)]
    [InlineData("2026-11-11", false)]   // gələcək (Pims 1665 halı)
    [InlineData("2026-10-02", false)]   // bu gün
    [InlineData("2021-01-01", false)]   // 5 yaş
    [InlineData("2019-10-03", false)]   // 7-yə 1 gün qalıb
    [InlineData("2019-10-02", true)]    // tam 7 yaş
    [InlineData("1990-05-10", true)]
    [InlineData("1900-01-01", true)]
    [InlineData("1899-12-31", false)]   // iiko alt həddi
    public void DobInput_Valid(string? dob, bool expected)
        => BirthDateRules.IsValidInput(dob == null ? null : DateTime.Parse(dob), Today).Should().Be(expected);

    // iiko-ya göndərmə aralığı: 1900-01-01 ≤ DOB ≤ bu gün (köhnə 5 yaş qeydi göndərilir, gələcək göndərilmir)
    [Theory]
    [InlineData(null, false)]
    [InlineData("2021-01-01", true)]
    [InlineData("2026-10-02", true)]
    [InlineData("2026-10-03", false)]
    [InlineData("2026-11-11", false)]
    [InlineData("1899-12-31", false)]
    public void DobPosRange(string? dob, bool expected)
        => BirthDateRules.IsWithinPosRange(dob == null ? null : DateTime.Parse(dob), Today).Should().Be(expected);

    [Fact]
    public void MaxDobForInput_IsSevenYearsAgo()
        => BirthDateRules.MaxDobForInput(Today).Should().Be(new DateTime(2019, 10, 2));

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

    // ---------- v3 (2026-10-06): N gün əvvəl + hədiyyə növü ----------

    [Fact]
    public void BirthdayKeysInRange_YearBoundary_AndFeb29()
    {
        // 28 dek + 7 gün → 28..31 dek + 1..4 yan = 8 açar
        var keys = BirthDateRules.BirthdayKeysInRange(new DateTime(2026, 12, 28), 7);
        keys.Should().HaveCount(8).And.Contain(new[] { 1228, 1231, 101, 104 }).And.NotContain(105);
        BirthDateRules.BirthdayKeysInRange(Today, 0).Should().Equal(1002);
        // 21 fev 2027 + 7 → 28 fev (uzun il deyil) → 29 fev də daxil: 8 gün + 1 = 9 açar
        BirthDateRules.BirthdayKeysInRange(new DateTime(2027, 2, 21), 7).Should().HaveCount(9).And.Contain(229);
        // 2028 uzun il: 28 fev-da 29 fev əlavə olunmur
        BirthDateRules.BirthdayKeysInRange(new DateTime(2028, 2, 21), 7).Should().HaveCount(8).And.NotContain(229);
    }

    [Fact]
    public void BirthdayWithin_RangeAndYear()
    {
        var today = new DateTime(2026, 12, 28);
        BirthDateRules.BirthdayWithin(new DateTime(1990, 1, 2), today, 7).Should().Be(new DateTime(2027, 1, 2));   // gələn il
        BirthDateRules.BirthdayWithin(new DateTime(1990, 1, 4), today, 7).Should().Be(new DateTime(2027, 1, 4));   // tam 7 gün
        BirthDateRules.BirthdayWithin(new DateTime(1990, 1, 5), today, 7).Should().BeNull();                        // 8 gün
        BirthDateRules.BirthdayWithin(new DateTime(1990, 12, 28), today, 7).Should().Be(today);                     // bu gün
        BirthDateRules.BirthdayWithin(new DateTime(1990, 12, 27), today, 7).Should().BeNull();                      // dünən → gələn il, aralıqdan kənar
        BirthDateRules.BirthdayWithin(new DateTime(1990, 12, 28), today, 0).Should().Be(today);
        BirthDateRules.BirthdayWithin(new DateTime(1990, 12, 29), today, 0).Should().BeNull();
        BirthDateRules.BirthdayWithin(new DateTime(2000, 2, 29), new DateTime(2027, 2, 21), 7).Should().Be(new DateTime(2027, 2, 28));
    }

    [Theory]
    [InlineData(BirthdayGiftType.Points, CardTemplateType.PointsBased, true)]
    [InlineData(BirthdayGiftType.Points, CardTemplateType.TierBased, true)]
    [InlineData(BirthdayGiftType.Points, CardTemplateType.ProgressBased, false)]
    [InlineData(BirthdayGiftType.Stamps, CardTemplateType.ProgressBased, true)]
    [InlineData(BirthdayGiftType.Stamps, CardTemplateType.PointsBased, false)]
    [InlineData(BirthdayGiftType.FreeItem, CardTemplateType.ProgressBased, true)]
    [InlineData(BirthdayGiftType.FreeItem, CardTemplateType.TierBased, false)]
    public void GiftMath_IsCompatible(BirthdayGiftType type, CardTemplateType card, bool expected)
        => BirthdayGiftMath.IsCompatible(type, card).Should().Be(expected);

    [Theory]
    [InlineData(BirthdayGiftType.Points, 2.5, 10, 2.5)]
    [InlineData(BirthdayGiftType.Points, 2.567, 10, 2.57)]
    [InlineData(BirthdayGiftType.Stamps, 2.9, 10, 2)]
    [InlineData(BirthdayGiftType.FreeItem, 1, 10, 10)]
    [InlineData(BirthdayGiftType.FreeItem, 2, 8, 16)]
    [InlineData(BirthdayGiftType.FreeItem, 1, 0, 1)]
    public void GiftMath_Delta(BirthdayGiftType type, decimal value, int steps, decimal expected)
        => BirthdayGiftMath.Delta(type, value, steps).Should().Be(expected);

    [Fact]
    public void GiftMath_Units_AndExpiry()
    {
        BirthdayGiftMath.Units(BirthdayGiftType.Points, 2.567m).Should().Be(2.57m);
        BirthdayGiftMath.Units(BirthdayGiftType.FreeItem, 1.9m).Should().Be(1);
        var now = new DateTime(2026, 10, 6, 6, 0, 0, DateTimeKind.Utc);
        BirthdayGiftMath.ExpiryUtc(now, 3, 7).Should().Be(now.AddDays(10));   // ad gününə 3 gün + 7 gün müddət
        BirthdayGiftMath.ExpiryUtc(now, 0, 7).Should().Be(now.AddDays(7));
        BirthdayGiftMath.ExpiryUtc(now, -2, 7).Should().Be(now.AddDays(7));
    }

    [Fact]
    public void GiftText_ByType_AndCustomLabel()
    {
        NotificationTemplate.GiftText(5, BirthdayGiftType.Points, null, null).Should().Be("5 ₼");
        NotificationTemplate.GiftText(2.5m, BirthdayGiftType.Points, "Pulsuz içki", null).Should().Be("2,5 ₼");
        NotificationTemplate.GiftText(2, BirthdayGiftType.Stamps, null, null).Should().Be("2 möhür");
        NotificationTemplate.GiftText(1, BirthdayGiftType.FreeItem, "Kofe", null).Should().Be("1 Kofe");
        NotificationTemplate.GiftText(1, BirthdayGiftType.FreeItem, null, null).Should().Be("1 Pulsuz içki");
        NotificationTemplate.GiftText(1, BirthdayGiftType.FreeItem, " ", null).Should().Be("1 Pulsuz içki");
        NotificationTemplate.GiftText(5, BirthdayGiftType.Points, null, " 1 kofe ").Should().Be("1 kofe");
        NotificationTemplate.GiftText(5, BirthdayGiftType.Points, null, "   ").Should().Be("5 ₼");
    }

    [Fact]
    public void Template_Tarix_AndGiftPlaceholder()
    {
        NotificationTemplate.Render("{ad}, {tarix} tarixində ad gününüz var!", "Leyla", null, null, "04.01").Should().Be("Leyla, 04.01 tarixində ad gününüz var!");
        NotificationTemplate.Render("{TARIX} gün", null, null, null, null).Should().Be("gün");
        NotificationTemplate.HasGiftPlaceholder("Sizə {hədiyyə} hədiyyə").Should().BeTrue();
        NotificationTemplate.HasGiftPlaceholder("{HEDIYYE}").Should().BeTrue();
        NotificationTemplate.HasGiftPlaceholder("Salam {ad}").Should().BeFalse();
        NotificationTemplate.HasGiftPlaceholder(null).Should().BeFalse();
    }

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

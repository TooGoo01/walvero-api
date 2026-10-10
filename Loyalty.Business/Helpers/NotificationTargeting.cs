using System.Text.Json;
using System.Text.Json.Serialization;
using Loyalty.DataAccess.Enums;

namespace Loyalty.Business.Helpers
{
    /// <summary>
    /// Bildiriş auditoriyası filtri (tenant admin "Yeni bildiriş" + planlı bildiriş). Bütün sahələr istəyə bağlıdır;
    /// boş filtr = tenant-ın bütün aktiv müştəriləri.
    /// </summary>
    public sealed class AudienceFilterDto
    {
        public int? BranchId { get; set; }
        public int? CustomerId { get; set; }
        /// <summary>Gender kodları: 0 Kişi, 1 Qadın, 2 Digər. Boş = hamısı.</summary>
        public List<int>? Genders { get; set; }
        public int? AgeMin { get; set; }
        public int? AgeMax { get; set; }
        /// <summary>true → cinsi/yaşı bilinməyənlər də (cins/yaş filtri aktivdirsə) daxil edilir.</summary>
        public bool IncludeUnknown { get; set; }
        /// <summary>Son N gündə əməliyyatı olanlar.</summary>
        public int? ActiveWithinDays { get; set; }
        /// <summary>N gündür əməliyyatı olmayanlar (heç gəlməyib, amma N gündən çoxdur qeydiyyatdadır — daxil).</summary>
        public int? InactiveForDays { get; set; }
        public decimal? MinBalance { get; set; }
        public decimal? MaxBalance { get; set; }
        /// <summary>true → hazır (istifadə olunmamış) möhür hədiyyəsi olanlar.</summary>
        public bool? HasReadyGift { get; set; }
        /// <summary>"all" | "app" | "wallet"</summary>
        public string? Channel { get; set; }

        public static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        public string ToJson() => JsonSerializer.Serialize(this, JsonOptions);

        public static AudienceFilterDto Parse(string? json)
        {
            if (string.IsNullOrWhiteSpace(json)) return new AudienceFilterDto();
            try { return JsonSerializer.Deserialize<AudienceFilterDto>(json, JsonOptions) ?? new AudienceFilterDto(); }
            catch { return new AudienceFilterDto(); }
        }

        public bool HasGenderFilter => Genders is { Count: > 0 };
        public bool HasAgeFilter => AgeMin.HasValue || AgeMax.HasValue;
        public string EffectiveChannel => Channel is "app" or "wallet" ? Channel : "all";

        /// <summary>Validasiya xətası (null = düzgün).</summary>
        public string? Validate()
        {
            if (Genders != null && Genders.Any(g => g is < 0 or > 2)) return "Cins dəyəri yanlışdır.";
            if (AgeMin is < 0 or > 120 || AgeMax is < 0 or > 120) return "Yaş 0–120 aralığında olmalıdır.";
            if (AgeMin.HasValue && AgeMax.HasValue && AgeMin > AgeMax) return "Minimum yaş maksimumdan böyük ola bilməz.";
            if (ActiveWithinDays is <= 0 or > 3650 || InactiveForDays is <= 0 or > 3650) return "Gün sayı 1–3650 aralığında olmalıdır.";
            if (ActiveWithinDays.HasValue && InactiveForDays.HasValue) return "Aktivlik filtri: ya 'son N gündə gələnlər', ya da 'N gündür gəlməyənlər'.";
            if (MinBalance < 0 || MaxBalance < 0) return "Balans mənfi ola bilməz.";
            if (MinBalance.HasValue && MaxBalance.HasValue && MinBalance > MaxBalance) return "Minimum balans maksimumdan böyük ola bilməz.";
            return null;
        }

        /// <summary>İnsan oxuya bilən xülasə (tarixçə və planlı siyahı üçün).</summary>
        public string Summary(string? branchName = null, string? customerName = null)
        {
            var parts = new List<string>();
            if (CustomerId.HasValue) parts.Add(customerName != null ? $"Fərdi: {customerName}" : "Fərdi müştəri");
            if (BranchId.HasValue) parts.Add(branchName != null ? $"Filial: {branchName}" : "Filial");
            if (HasGenderFilter)
                parts.Add(string.Join("/", Genders!.Distinct().OrderBy(g => g).Select(g => g switch { 0 => "Kişi", 1 => "Qadın", _ => "Digər" })));
            if (HasAgeFilter)
                parts.Add(AgeMin.HasValue && AgeMax.HasValue ? $"{AgeMin}–{AgeMax} yaş" : AgeMin.HasValue ? $"{AgeMin}+ yaş" : $"≤{AgeMax} yaş");
            if (ActiveWithinDays.HasValue) parts.Add($"son {ActiveWithinDays} gündə gələnlər");
            if (InactiveForDays.HasValue) parts.Add($"{InactiveForDays}+ gündür gəlməyənlər");
            if (MinBalance.HasValue || MaxBalance.HasValue)
                parts.Add(MinBalance.HasValue && MaxBalance.HasValue ? $"balans {MinBalance}–{MaxBalance}" : MinBalance.HasValue ? $"balans ≥ {MinBalance}" : $"balans ≤ {MaxBalance}");
            if (HasReadyGift == true) parts.Add("hədiyyəsi hazır olanlar");
            if (EffectiveChannel == "app") parts.Add("yalnız app");
            if (EffectiveChannel == "wallet") parts.Add("yalnız wallet");
            if ((HasGenderFilter || HasAgeFilter) && IncludeUnknown) parts.Add("bilinməyənlər daxil");
            return parts.Count == 0 ? "Bütün müştərilər" : string.Join(" · ", parts);
        }
    }

    /// <summary>Doğum tarixi, yaş və ad günü qaydaları (pure — testlənir).</summary>
    public static class BirthDateRules
    {
        /// <summary>Minimum yaş: giriş validasiyası (web/app/import) və ad günü filtri üçün eyni qayda (2026-10-09: 7).</summary>
        public const int MinPlausibleAge = 7;
        /// <summary>Alt hədd — iiko 1900-dən əvvəlki tarixi qəbul etmir.</summary>
        public static readonly DateTime MinDob = new(1900, 1, 1);

        /// <summary>Seçilə bilən ən son doğum tarixi: bu gün − MinPlausibleAge il.</summary>
        public static DateTime MaxDobForInput(DateTime today) => today.Date.AddYears(-MinPlausibleAge);

        /// <summary>Giriş validasiyası: boş ola bilər; doludursa 1900-01-01 ≤ DOB ≤ bu gün − 7 il (gələcək tarix və 7 yaşdan kiçik rədd).</summary>
        public static bool IsValidInput(DateTime? dob, DateTime today)
        {
            if (!dob.HasValue) return true;
            var d = dob.Value.Date;
            return d >= MinDob && d <= MaxDobForInput(today);
        }

        /// <summary>POS-a (iiko) göndərilə bilər: 1900-01-01 ≤ DOB ≤ bu gün (iiko qaydası); kənar olanda sahə göndərilmir.</summary>
        public static bool IsWithinPosRange(DateTime? dob, DateTime today)
            => dob.HasValue && dob.Value.Date >= MinDob && dob.Value.Date <= today.Date;

        /// <summary>DOB istifadəyə yararlıdır: yaş ≥ MinPlausibleAge və DOB qeydiyyat günü ilə eyni deyil (picker default-u).</summary>
        public static bool IsUsable(DateTime? dob, DateTime regDate, DateTime localToday)
        {
            if (!dob.HasValue) return false;
            var d = dob.Value.Date;
            if (d > localToday.Date.AddYears(-MinPlausibleAge)) return false;
            if (d == regDate.Date) return false;
            return true;
        }

        /// <summary>Yaşı ən azı <paramref name="minAge"/> olanların DOB üst həddi (DOB ≤ bu tarix).</summary>
        public static DateTime MaxDobForMinAge(DateTime localToday, int minAge) => localToday.Date.AddYears(-minAge);

        /// <summary>Yaşı ən çox <paramref name="maxAge"/> olanların DOB alt həddi (DOB &gt; bu tarix).</summary>
        public static DateTime MinDobExclusiveForMaxAge(DateTime localToday, int maxAge) => localToday.Date.AddYears(-(maxAge + 1));

        public static int Age(DateTime dob, DateTime localToday)
        {
            var age = localToday.Year - dob.Year;
            if (dob.Date > localToday.Date.AddYears(-age)) age--;
            return age;
        }

        /// <summary>Bu gün ad günüdürmü? 29 fevral doğulanlar qeyri-uzun ildə 28 fevralda təbrik olunur.</summary>
        public static bool IsBirthday(DateTime dob, DateTime localToday)
        {
            if (dob.Month == localToday.Month && dob.Day == localToday.Day) return true;
            return dob.Month == 2 && dob.Day == 29 && localToday.Month == 2 && localToday.Day == 28 && !DateTime.IsLeapYear(localToday.Year);
        }

        /// <summary>SQL-də ad günü axtarışı üçün (ay, gün) cütləri: bu gün + qeyri-uzun ilin 28 fevralında 29 fevral.</summary>
        public static IReadOnlyList<(int Month, int Day)> BirthdayKeys(DateTime localToday)
        {
            var keys = new List<(int, int)> { (localToday.Month, localToday.Day) };
            if (localToday.Month == 2 && localToday.Day == 28 && !DateTime.IsLeapYear(localToday.Year)) keys.Add((2, 29));
            return keys;
        }

        /// <summary>Növbəti <paramref name="days"/> gündə ad günü olanlar üçün tarix (bu il/gələn il).</summary>
        public static DateTime NextBirthday(DateTime dob, DateTime localToday)
        {
            DateTime At(int year) => dob.Month == 2 && dob.Day == 29 && !DateTime.IsLeapYear(year)
                ? new DateTime(year, 2, 28) : new DateTime(year, dob.Month, dob.Day);
            var thisYear = At(localToday.Year);
            return thisYear >= localToday.Date ? thisYear : At(localToday.Year + 1);
        }

        /// <summary>
        /// [bu gün, bu gün + daysBefore] aralığındakı bütün günlər üçün SQL (ay*100+gün) açarları (29 fevral qaydası daxil).
        /// Aralıq skanı: ayar 0→7 dəyişəndə və ya job bir gün işləməyəndə heç kim atlanmır — unikal (CustomerId, Year) bir göndəriş verir.
        /// </summary>
        public static IReadOnlyList<int> BirthdayKeysInRange(DateTime localToday, int daysBefore)
        {
            var keys = new HashSet<int>();
            for (var i = 0; i <= Math.Max(0, daysBefore); i++)
                foreach (var (m, d) in BirthdayKeys(localToday.Date.AddDays(i)))
                    keys.Add(m * 100 + d);
            return keys.ToList();
        }

        /// <summary>Aralıq skanında müştərinin real ad günü (bu il/gələn il); aralıqdan kənardırsa null.</summary>
        public static DateTime? BirthdayWithin(DateTime dob, DateTime localToday, int daysBefore)
        {
            var next = NextBirthday(dob, localToday);
            return (next - localToday.Date).TotalDays <= Math.Max(0, daysBefore) ? next : null;
        }
    }

    /// <summary>Ad günü hədiyyəsi riyaziyyatı (pure — testlənir).</summary>
    public static class BirthdayGiftMath
    {
        /// <summary>Points yalnız xal/tier kartına, Stamps/FreeItem yalnız möhür kartına yazılır.</summary>
        public static bool IsCompatible(BirthdayGiftType type, CardTemplateType effectiveType) =>
            type == BirthdayGiftType.Points
                ? effectiveType != CardTemplateType.ProgressBased
                : effectiveType == CardTemplateType.ProgressBased;

        /// <summary>Göstərmə vahidi: Points ₼ (0.01 dəqiqlik), Stamps/FreeItem tam ədəd.</summary>
        public static decimal Units(BirthdayGiftType type, decimal value) =>
            type == BirthdayGiftType.Points ? Math.Round(value, 2) : Math.Floor(value);

        /// <summary>Karta yazılan delta: Points ₼, Stamps möhür, FreeItem məhsul × TotalSteps (CompletedCycles +N).</summary>
        public static decimal Delta(BirthdayGiftType type, decimal value, int totalSteps) => type switch
        {
            BirthdayGiftType.Stamps => Math.Floor(value),
            BirthdayGiftType.FreeItem => Math.Floor(value) * Math.Max(1, totalSteps),
            _ => Math.Round(value, 2)
        };

        /// <summary>Bonus müddəti ad günündən hesablanır: indi + (ad gününə qalan gün) + müddət.</summary>
        public static DateTime ExpiryUtc(DateTime nowUtc, int daysUntilBirthday, int expiryDays) =>
            nowUtc.AddDays(Math.Max(0, daysUntilBirthday) + expiryDays);
    }

    /// <summary>Mesaj şablonu: {ad}, {restoran}, {hədiyyə} (həmçinin {hediyye}), {tarix}.</summary>
    public static class NotificationTemplate
    {
        public static string Render(string template, string? firstName, string? tenantName, string? giftText, string? dateText = null)
        {
            if (string.IsNullOrEmpty(template)) return template;
            var s = template
                .Replace("{ad}", string.IsNullOrWhiteSpace(firstName) ? "" : firstName.Trim(), StringComparison.OrdinalIgnoreCase)
                .Replace("{restoran}", tenantName ?? "", StringComparison.OrdinalIgnoreCase)
                .Replace("{hədiyyə}", giftText ?? "", StringComparison.OrdinalIgnoreCase)
                .Replace("{hediyye}", giftText ?? "", StringComparison.OrdinalIgnoreCase)
                .Replace("{tarix}", dateText ?? "", StringComparison.OrdinalIgnoreCase);
            // boş placeholder-dən qalan "Salam , ..." kimi artıq boşluqları təmizlə
            while (s.Contains("  ")) s = s.Replace("  ", " ");
            return s.Replace(" ,", ",").Replace(" !", "!").Trim();
        }

        public static bool HasPersonalPlaceholder(string? s) =>
            !string.IsNullOrEmpty(s) && s.Contains("{ad}", StringComparison.OrdinalIgnoreCase);

        public static bool HasGiftPlaceholder(string? s) =>
            !string.IsNullOrEmpty(s)
            && (s.Contains("{hədiyyə}", StringComparison.OrdinalIgnoreCase) || s.Contains("{hediyye}", StringComparison.OrdinalIgnoreCase));

        /// <summary>Hədiyyə mətni: xal kartında "5 ₼", möhür kartında "2 möhür".</summary>
        public static string GiftText(decimal value, bool isStamp) =>
            isStamp ? $"{decimal.ToInt32(Math.Floor(value))} möhür" : $"{value.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture).Replace('.', ',')} ₼";

        /// <summary>
        /// Növə görə hədiyyə mətni: customLabel doludursa o ("1 kofe"); Points "5 ₼"; Stamps "2 möhür";
        /// FreeItem "1 Pulsuz içki" (şablonun FreeRewardLabel-i, app-ın freeRewardText formatı ilə eyni).
        /// </summary>
        public static string GiftText(decimal units, BirthdayGiftType type, string? freeRewardLabel, string? customLabel)
        {
            if (!string.IsNullOrWhiteSpace(customLabel)) return customLabel.Trim();
            return type switch
            {
                BirthdayGiftType.Stamps => GiftText(units, true),
                BirthdayGiftType.FreeItem => $"{decimal.ToInt32(Math.Floor(units))} {(string.IsNullOrWhiteSpace(freeRewardLabel) ? "Pulsuz içki" : freeRewardLabel.Trim())}",
                _ => GiftText(units, false)
            };
        }
    }

    /// <summary>Müddətli bonusun istifadə olunmamış hissəsi ("bonus əvvəl xərclənir").</summary>
    public static class BonusExpiryMath
    {
        public static decimal Unspent(decimal grantAmount, decimal spentSinceGrant, decimal currentPoints)
        {
            var left = grantAmount - Math.Max(0, spentSinceGrant);
            if (left <= 0) return 0;
            return Math.Max(0, Math.Min(left, currentPoints));
        }
    }

    /// <summary>Tenant vaxt zonası: TenantLocation.TimeZone → ReceiptTimeZoneId → Azerbaijan Standard Time.</summary>
    public static class TenantClock
    {
        public const string DefaultTimeZoneId = "Azerbaijan Standard Time";

        public static TimeZoneInfo Resolve(string? locationTz, string? receiptTz)
        {
            foreach (var id in new[] { locationTz, receiptTz, DefaultTimeZoneId })
            {
                if (string.IsNullOrWhiteSpace(id)) continue;
                try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
                catch { /* növbəti */ }
            }
            return TimeZoneInfo.Utc;
        }

        public static DateTime LocalNow(TimeZoneInfo tz, DateTime? utcNow = null)
            => TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utcNow ?? DateTime.UtcNow, DateTimeKind.Utc), tz);

        /// <summary>Tenant yerli vaxtı (Kind fərq etmir) → UTC.</summary>
        public static DateTime ToUtc(DateTime local, TimeZoneInfo tz)
            => TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(local, DateTimeKind.Unspecified), tz);

        public static DateTime ToLocal(DateTime utc, TimeZoneInfo tz)
            => TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), tz);

        /// <summary>Tenant yerli ayının başlanğıcı və sonu (UTC).</summary>
        public static (DateTime StartUtc, DateTime EndUtc) MonthRangeUtc(TimeZoneInfo tz, DateTime? utcNow = null)
        {
            var local = LocalNow(tz, utcNow);
            var start = new DateTime(local.Year, local.Month, 1);
            return (ToUtc(start, tz), ToUtc(start.AddMonths(1), tz));
        }
    }
}

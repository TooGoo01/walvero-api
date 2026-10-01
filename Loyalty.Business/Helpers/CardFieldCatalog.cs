using Loyalty.Business.Dtos.Post.Main;
using Loyalty.DataAccess.Enums;

namespace Loyalty.Business.Helpers
{
    /// <summary>Apple storeCard yuvaları (Google sətirləri bundan törəyir: Header+Primary → 1, Secondary → 2, Auxiliary → 3, Back → detallar).</summary>
    public enum CardFieldSlot { Hidden = 0, Header = 1, Primary = 2, Secondary = 3, Auxiliary = 4, Back = 5 }

    /// <summary>Google banner yeri: Bottom = obyekt heroImage (sətir tutmur), Top = image modulu 1-ci sətirdə (1 sətir tutur), None = yox.</summary>
    public enum GoogleBannerPosition { Bottom = 0, Top = 1, None = 2 }

    public enum CardPlatform { Apple = 0, Google = 1 }

    /// <summary>Admin-in bir sahə üçün seçimi (DesignJson v2 → Fields).</summary>
    public sealed class CardFieldSetting
    {
        public string Id { get; set; } = "";
        public CardFieldSlot Slot { get; set; } = CardFieldSlot.Hidden;
        public int Order { get; set; }
        /// <summary>Etiket override; boş = default (Faza 1 etiketləri / sahə adı).</summary>
        public string? Label { get; set; }
    }

    /// <summary>Hesablanmış sahə dəyəri. DefaultLabel null = etiketsiz.</summary>
    public sealed record CardFieldValue(string Value, string? DefaultLabel);

    /// <summary>
    /// Kart sahələrinin kataloqu: id-lər, template tipinə görə default yuvalar (= bugünkü görünüş), limitlər, dəyər hesablanması,
    /// Apple/Google barkod tipi xəritəsi, Google modul id-ləri. Apple builder, Google builder, preview və admin eyni mənbədən.
    /// </summary>
    public static class CardFieldCatalog
    {
        public const string CustomerName = "customerName";
        public const string Balance = "balance";
        public const string Cashback = "cashback";
        public const string Tier = "tier";
        public const string CardNumber = "cardNumber";
        public const string PointsExpiry = "pointsExpiry";
        public const string LifetimePoints = "lifetimePoints";
        public const string ProgramName = "programName";
        public const string RewardRule = "rewardRule";
        public const string FreeReward = "freeReward";

        public static readonly string[] AllIds =
            { CustomerName, Balance, Cashback, Tier, CardNumber, PointsExpiry, LifetimePoints, ProgramName, RewardRule, FreeReward };

        /// <summary>Hansı sahələr hansı template tipində mənalıdır (admin UI yalnız bunları göstərir).</summary>
        public static bool IsApplicable(string id, CardTemplateType type) => type == CardTemplateType.ProgressBased
            ? id is not (Balance or Cashback or Tier or LifetimePoints)
            : id is not (RewardRule or FreeReward) && (id != Tier || type == CardTemplateType.TierBased);

        /// <summary>Apple yuva limitləri (storeCard).</summary>
        public static int SlotLimit(CardFieldSlot slot) => slot switch
        {
            CardFieldSlot.Header => 3,
            CardFieldSlot.Primary => 1,
            CardFieldSlot.Secondary => 4,
            CardFieldSlot.Auxiliary => 4,
            _ => int.MaxValue
        };

        /// <summary>Google: hər sətirdə ən çox 3 element (API sxemi: one/two/threeItems).</summary>
        public const int GoogleRowItemLimit = 3;
        /// <summary>Google sənədi 3 sətir deyir; Faza 0 sınağı (2026-10-01) 5 sətiri qəbul etdi və render olundu (prod Progress class-ı da 5 sətirlidir).</summary>
        public const int GoogleRowLimit = 5;
        public const int GoogleRowDocLimit = 3;

        /// <summary>Bugünkü (Faza 1) yerləşmə — admin heç nə seçməyibsə.</summary>
        public static List<CardFieldSetting> DefaultFields(CardTemplateType type)
        {
            var list = new List<CardFieldSetting>();
            if (type == CardTemplateType.ProgressBased)
            {
                list.Add(new() { Id = RewardRule, Slot = CardFieldSlot.Primary, Order = 1 });
                list.Add(new() { Id = CustomerName, Slot = CardFieldSlot.Secondary, Order = 1 });
                list.Add(new() { Id = FreeReward, Slot = CardFieldSlot.Secondary, Order = 2 });
            }
            else
            {
                list.Add(new() { Id = Balance, Slot = CardFieldSlot.Header, Order = 1 });
                list.Add(new() { Id = CustomerName, Slot = CardFieldSlot.Secondary, Order = 1 });
                list.Add(new() { Id = Cashback, Slot = CardFieldSlot.Auxiliary, Order = 1 });
            }
            var order = 100;
            foreach (var id in AllIds)
                if (list.All(f => f.Id != id))
                    list.Add(new() { Id = id, Slot = CardFieldSlot.Hidden, Order = order++ });
            return list;
        }

        /// <summary>Saxlanmış seçimləri default-ların üstünə qoy; naməlum id-lər atılır, çatışmayanlar gizli; tipə uyğun olmayanlar gizli; yuva limitləri (artıq → Back).</summary>
        public static List<CardFieldSetting> Effective(IEnumerable<CardFieldSetting>? saved, CardTemplateType type, out List<string> warnings)
        {
            warnings = new List<string>();
            var result = DefaultFields(type);
            if (saved != null)
            {
                var savedList = saved.Where(f => f != null && AllIds.Contains(f.Id)).ToList();
                if (savedList.Count > 0)
                {
                    foreach (var f in result) { f.Slot = CardFieldSlot.Hidden; f.Order = 100 + Array.IndexOf(AllIds, f.Id); }
                    foreach (var sf in savedList)
                    {
                        var f = result.First(x => x.Id == sf.Id);
                        f.Slot = Enum.IsDefined(typeof(CardFieldSlot), sf.Slot) ? sf.Slot : CardFieldSlot.Hidden;
                        f.Order = sf.Order;
                        f.Label = string.IsNullOrWhiteSpace(sf.Label) ? null : sf.Label.Trim();
                    }
                }
            }
            foreach (var f in result.Where(f => !IsApplicable(f.Id, type) && f.Slot != CardFieldSlot.Hidden))
                f.Slot = CardFieldSlot.Hidden;
            foreach (var slot in new[] { CardFieldSlot.Header, CardFieldSlot.Primary, CardFieldSlot.Secondary, CardFieldSlot.Auxiliary })
            {
                var inSlot = result.Where(f => f.Slot == slot).OrderBy(f => f.Order).ToList();
                var limit = SlotLimit(slot);
                foreach (var extra in inSlot.Skip(limit))
                {
                    extra.Slot = CardFieldSlot.Back;
                    warnings.Add($"{extra.Id}: {slot} yuvası doludur (max {limit}) → arxa üzə keçirildi.");
                }
            }
            return result.OrderBy(f => f.Slot).ThenBy(f => f.Order).ToList();
        }

        public static bool IsVisible(IEnumerable<CardFieldSetting> fields, string id)
            => fields.Any(f => f.Id == id && f.Slot != CardFieldSlot.Hidden);

        /// <summary>Xal bitmə tarixi mətni — tenant yerli vaxtı (ITimeZoneService), yoxdursa UTC.</summary>
        public static async Task<string?> FormatExpiryAsync(Services.Abstractions.Main.ITimeZoneService? tz, int tenantId, DateTime? expiresAtUtc, CancellationToken ct)
        {
            if (!expiresAtUtc.HasValue) return null;
            var utc = DateTime.SpecifyKind(expiresAtUtc.Value, DateTimeKind.Utc);
            if (tz != null)
            {
                try
                {
                    var (local, _) = await tz.GetTenantLocalTimeAsync(tenantId, utc, ct);
                    return local.ToString("dd.MM.yyyy HH:mm");
                }
                catch { /* UTC fallback */ }
            }
            return utc.ToString("dd.MM.yyyy HH:mm") + " UTC";
        }

        /// <summary>Cashback faizləri (PointsBased → həmişə Bronze; TierBased → hədəflərə görə; boş faiz aşağı pilləyə düşür).</summary>
        public static (int Percent, int PercentCash) CashbackPercents(CardTemplateSnapshot s, decimal points)
        {
            var bronze = (s.BronzePercent ?? 0, s.BronzePercentCash ?? 0);
            if (s.TemplateType != CardTemplateType.TierBased) return bronze;
            var silver = (s.SilverPercent ?? bronze.Item1, s.SilverPercentCash ?? bronze.Item2);
            var gold = (s.GoldPercent ?? silver.Item1, s.GoldPercentCash ?? silver.Item2);
            var plat = (s.PlatinumPercent ?? gold.Item1, s.PlatinumPercentCash ?? gold.Item2);
            if (s.PlatinumThreshold.HasValue && points >= s.PlatinumThreshold.Value) return plat;
            if (s.GoldThreshold.HasValue && points >= s.GoldThreshold.Value) return gold;
            if (s.SilverThreshold.HasValue && points >= s.SilverThreshold.Value) return silver;
            return bronze;
        }

        public static string? TierName(CardTemplateSnapshot s, decimal points)
        {
            if (s.TemplateType != CardTemplateType.TierBased) return null;
            if (s.PlatinumThreshold.HasValue && points >= s.PlatinumThreshold.Value) return "Platinum";
            if (s.GoldThreshold.HasValue && points >= s.GoldThreshold.Value) return "Gold";
            if (s.SilverThreshold.HasValue && points >= s.SilverThreshold.Value) return "Silver";
            return "Bronze";
        }

        /// <summary>
        /// Bütün sahə dəyərləri (maskalanmış giriş dəyərləri ilə). Boş dəyər = sahə göstərilmir.
        /// Platforma fərqləri bugünkü davranışla eynidir: Progress müştəri adı etiketsiz; freeReward Apple-da "{label}: N", Google-da header=label/body=N;
        /// rewardRule Apple-da BÖYÜK hərf, Google-da olduğu kimi.
        /// </summary>
        public static Dictionary<string, CardFieldValue> BuildValues(
            CardTemplateSnapshot s, string? customerFullName, string? barcodeValue,
            decimal displayPoints, int displaySteps, int displayCycles, decimal lifetimePoints, string? pointsExpiryText,
            CardPlatform platform)
        {
            var d = s.Design ?? new CardDesignSettings();
            var isProgress = s.TemplateType == CardTemplateType.ProgressBased;
            var (percent, percentCash) = CashbackPercents(s, displayPoints);
            var currency = string.IsNullOrWhiteSpace(s.Currency) ? "AZN" : s.Currency;
            var freeLabel = string.IsNullOrWhiteSpace(s.FreeRewardLabel) ? "bonus" : s.FreeRewardLabel;

            string cashbackLabel, cashbackText;
            if (s.PayMentType) { cashbackLabel = $"{d.EffectiveCashbackLabel}:{percent}%"; cashbackText = $" Nağd ödənişdə {percentCash}%"; }
            else { cashbackLabel = d.EffectiveCashbackLabel; cashbackText = $"{percent}%"; }

            var rule = s.RewardDescription ?? "";
            return new Dictionary<string, CardFieldValue>
            {
                [CustomerName] = new(customerFullName ?? "", isProgress ? null : d.EffectiveCustomerLabel),
                [Balance] = new($"{displayPoints} {currency}", d.EffectiveBalanceLabel),
                [Cashback] = new(cashbackText, cashbackLabel),
                [Tier] = new(TierName(s, displayPoints) ?? "", "Tier"),
                [CardNumber] = new(barcodeValue ?? "", "Kart nömrəsi"),
                [PointsExpiry] = new(pointsExpiryText ?? "", "Bitmə tarixi"),
                [LifetimePoints] = new($"{lifetimePoints} {currency}", "Ümumi xal"),
                [ProgramName] = new(s.ProgramName ?? "", "Proqram"),
                [RewardRule] = new(platform == CardPlatform.Apple ? rule.ToUpperInvariant() : rule, null),
                [FreeReward] = platform == CardPlatform.Apple
                    ? new($"{freeLabel}: {displayCycles}", null)
                    : new(displayCycles.ToString(), freeLabel),
            };
        }

        /// <summary>Admin etiketi (PayMentType cashback-də faiz suffiksi qalır); null = etiketsiz.</summary>
        public static string? EffectiveLabel(CardFieldSetting f, CardFieldValue v, CardTemplateSnapshot s, decimal displayPoints)
        {
            if (string.IsNullOrWhiteSpace(f.Label)) return v.DefaultLabel;
            if (f.Id == Cashback && s.PayMentType) return $"{f.Label}:{CashbackPercents(s, displayPoints).Percent}%";
            return f.Label;
        }

        public static string AppleBarcodeFormat(BarcodeType? t) => t switch
        {
            BarcodeType.QR => "PKBarcodeFormatQR",
            BarcodeType.PDF417 => "PKBarcodeFormatPDF417",
            BarcodeType.Aztec => "PKBarcodeFormatAztec",
            _ => "PKBarcodeFormatCode128"
        };

        public static string GoogleBarcodeType(BarcodeType? t) => t switch
        {
            BarcodeType.QR => "QR_CODE",
            BarcodeType.PDF417 => "PDF_417",
            BarcodeType.Aztec => "AZTEC",
            _ => "CODE_128"
        };

        /// <summary>Apple pass.json field key-ləri (bugünkü açarlar saxlanılır).</summary>
        public static string AppleKey(string fieldId, CardTemplateType type) => fieldId switch
        {
            CustomerName => type == CardTemplateType.ProgressBased ? "rewardCard" : "name",
            RewardRule => "rule",
            FreeReward => "pulsuzIcki",
            _ => fieldId
        };

        /// <summary>Google textModulesData id-ləri — mövcud class sətirləri/obyektlərlə uyğun (Progress: rewardCard/pulsuzIcki/rewardDesc; Points: customer/balance/cashback).</summary>
        public static string GoogleModuleId(string fieldId, CardTemplateType type) => fieldId switch
        {
            CustomerName => type == CardTemplateType.ProgressBased ? "rewardCard" : "customer",
            RewardRule => "rewardDesc",
            FreeReward => "pulsuzIcki",
            _ => fieldId
        };

        /// <summary>Artıq istifadə olunmayan modul id-ləri — update-də silinir.</summary>
        public static readonly string[] LegacyModuleIds = { "features" };

        public const string GoogleImageRow = "__image__";
        public const string GoogleBarcodeRow = "__barcode__";

        /// <summary>"Sahələr" toxunulmayıbsa (Fields boş, GoogleBanner null) Google class-ı köhnə (bugünkü) sətir yerləşməsində qalır.</summary>
        public static bool IsLegacyGoogleLayout(CardDesignSettings? d)
            => d == null || ((d.Fields == null || d.Fields.Count == 0) && d.GoogleBanner == null);

        /// <summary>Bugünkü Google class sətirləri (prod-dakı class-larla eyni) — admin layout-u dəyişməyibsə dəyişmir.</summary>
        public static List<List<string>> LegacyGoogleRows(CardTemplateType type) => type == CardTemplateType.ProgressBased
            ? new List<List<string>>
            {
                new() { CustomerName }, new() { FreeReward }, new() { GoogleBarcodeRow }, new() { RewardRule }, new() { GoogleImageRow }
            }
            : new List<List<string>>
            {
                new() { GoogleImageRow }, new() { CustomerName, Balance }, new() { Cashback }, new() { GoogleBarcodeRow }
            };

        /// <summary>
        /// Google kart sətirləri: Header+Primary → sətir 1, Secondary → sətir 2, Auxiliary → sətir 3 (hər biri ≤3 element);
        /// banner Top → image modulu 1-ci sətirdə. Nəticə: sətir siyahısı (fieldId-lər; "__image__" = banner). Barkod native (sətir tutmur).
        /// </summary>
        public static List<List<string>> GoogleRows(IEnumerable<CardFieldSetting> effective, GoogleBannerPosition banner, out List<string> warnings)
        {
            var w = new List<string>();
            var vis = effective.Where(f => f.Slot != CardFieldSlot.Hidden && f.Slot != CardFieldSlot.Back).ToList();
            List<string> Take(Func<CardFieldSetting, bool> pred)
            {
                var items = vis.Where(pred).OrderBy(f => f.Order).Select(f => f.Id).ToList();
                if (items.Count > GoogleRowItemLimit)
                {
                    w.Add($"Google sətirində ən çox {GoogleRowItemLimit} element: {string.Join(",", items.Skip(GoogleRowItemLimit))} detallara düşür.");
                    items = items.Take(GoogleRowItemLimit).ToList();
                }
                return items;
            }
            var rows = new List<List<string>>();
            if (banner == GoogleBannerPosition.Top) rows.Add(new List<string> { GoogleImageRow });
            foreach (var r in new[]
            {
                Take(f => f.Slot is CardFieldSlot.Header or CardFieldSlot.Primary),
                Take(f => f.Slot == CardFieldSlot.Secondary),
                Take(f => f.Slot == CardFieldSlot.Auxiliary)
            })
                if (r.Count > 0) rows.Add(r);
            if (rows.Count > GoogleRowLimit)
            {
                w.Add($"Google: ən çox {GoogleRowLimit} sətir — sonuncu sətir(lər) detallara düşdü.");
                rows = rows.Take(GoogleRowLimit).ToList();
            }
            warnings = w;
            return rows;
        }
    }
}

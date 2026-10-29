using System.Text.Json;
using System.Text.Json.Serialization;

namespace Loyalty.Business.Dtos.Post.Main
{
    /// <summary>Kartda balans rəqəmi: Auto = pul valyutası (AZN/₼…) olduğu kimi, qalanları (məhsul/xal) tam ədəd.</summary>
    public enum BalanceFormat { Auto = 0, Integer = 1, Decimal = 2 }

    /// <summary>
    /// Kart dizayneri ayarları — CardTemplate.DesignJson-da versiyalı saxlanır.
    /// Boş/null sahə = platformaların bugünkü default mətni (mövcud kartlar dəyişmir).
    /// Faza 1: mətnlər. Faza 2/3 sahələri (görünürlük, barkod tipi, app layout) buraya əlavə olunur — sxem dəyişmir.
    /// </summary>
    public sealed class CardDesignSettings
    {
        public const int CurrentVersion = 2;

        public static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            WriteIndented = false
        };

        public int Version { get; set; } = CurrentVersion;

        /// <summary>Kart başlığı — Apple organizationName, Google programName. Boş → proqram adı.</summary>
        public string? Title { get; set; }
        /// <summary>Apple: logonun yanındakı mətn (logoText). Boş → göstərilmir.</summary>
        public string? LogoText { get; set; }
        /// <summary>Apple: pass təsviri (accessibility/description). Boş → "Loyalty Card".</summary>
        public string? Description { get; set; }
        /// <summary>Google: issuerName (başlığın üstündə kiçik). Boş → tenant şirkət adı.</summary>
        public string? IssuerName { get; set; }
        /// <summary>Balans etiketi — Apple header "Balance", Google loyaltyPoints.label + "BALANCE" modulu.</summary>
        public string? BalanceLabel { get; set; }
        /// <summary>Müştəri adı etiketi — Apple "Customer", Google "Customer" modulu.</summary>
        public string? CustomerLabel { get; set; }
        /// <summary>Cashback etiketi — Apple/Google "Cashback".</summary>
        public string? CashbackLabel { get; set; }
        /// <summary>Arxa üz / detallar başlığı. Boş → "Walvero Loyallıq".</summary>
        public string? AboutTitle { get; set; }
        /// <summary>Arxa üz / detallar mətni. Boş → default Walvero mətni. "-" → bölmə gizlədilir.</summary>
        public string? AboutText { get; set; }
        /// <summary>Sayt linki. Boş → https://www.walvero.com. "-" → link gizlədilir.</summary>
        public string? WebsiteUrl { get; set; }
        /// <summary>Link etiketi. Boş → "Veb-sayt" (Apple) / "Learn More" (Google).</summary>
        public string? WebsiteLabel { get; set; }

        // ---- v2 (Faza 2): sahə yuvaları / görünürlük / sıra, barkod tipi, Google banner yeri ----
        /// <summary>Sahə seçimləri; null/boş = template tipinin default yerləşməsi (CardFieldCatalog.DefaultFields).</summary>
        public List<Helpers.CardFieldSetting>? Fields { get; set; }
        /// <summary>Apple + Google barkod tipi; null = Code128.</summary>
        public DataAccess.Enums.BarcodeType? Barcode { get; set; }
        /// <summary>Google banner yeri; null = template default (Points/Tier → Top, Progress → Bottom).</summary>
        public Helpers.GoogleBannerPosition? GoogleBanner { get; set; }

        // ---- Balans görüntüsü + dəyişmə bildirişi ----
        /// <summary>Balans vahidi (görüntü) — məs. "məhsul" əvəzinə "ədəd". Boş → şablon valyutası (CardTemplate.Currency məntiqdə dəyişmir).</summary>
        public string? BalanceUnit { get; set; }
        /// <summary>Balans rəqəm formatı; null = Auto (pul valyutası deyilsə tam ədəd).</summary>
        public BalanceFormat? BalanceFormat { get; set; }
        /// <summary>Balans dəyişəndə bildiriş (Apple changeMessage, Google TEXT_AND_NOTIFY). null/false = söndürülü.</summary>
        public bool? BalanceNotify { get; set; }
        /// <summary>Bildiriş mətni; "%@" = yeni dəyər. Boş → DefaultBalanceChangeText.</summary>
        public string? BalanceChangeText { get; set; }

        [JsonIgnore] public DataAccess.Enums.BarcodeType EffectiveBarcode => Barcode ?? DataAccess.Enums.BarcodeType.Code128;

        public Helpers.GoogleBannerPosition EffectiveGoogleBanner(DataAccess.Enums.CardTemplateType type)
            => GoogleBanner ?? (type == DataAccess.Enums.CardTemplateType.ProgressBased ? Helpers.GoogleBannerPosition.Bottom : Helpers.GoogleBannerPosition.Top);

        /// <summary>Effektiv sahə siyahısı (default + saxlanmış, limitlər tətbiq olunmuş).</summary>
        public List<Helpers.CardFieldSetting> EffectiveFields(DataAccess.Enums.CardTemplateType type)
            => Helpers.CardFieldCatalog.Effective(Fields, type, out _);

        // ---- Defaults (bugünkü hardcoded dəyərlər) ----
        public const string DefaultDescription = "Loyalty Card";
        public const string DefaultBalanceLabel = "Balance";
        public const string DefaultCustomerLabel = "Customer";
        public const string DefaultCashbackLabel = "Cashback";
        public const string DefaultAboutTitle = "Walvero Loyallıq";
        public const string DefaultAboutText =
            "POS ilə inteqrasiya olunan sadiqlik proqramı. Müştəri bağlılığını artırın və təkrar alışları təşviq edin.\n\n" +
            "• Fərdiləşdirilmiş mükafatlar\n• Avtomatlaşdırılmış izləmə (POS)\n• Hədəfli kampaniyalar\n• Analitika və insight-lar";
        public const string DefaultWebsiteUrl = "https://www.walvero.com";
        public const string DefaultWebsiteLabel = "Veb-sayt";
        public const string DefaultBalanceChangeText = "Balansınız: %@";
        public const string ChangeValuePlaceholder = "%@";
        /// <summary>Admin bölməni gizlətmək üçün bu dəyəri yazır.</summary>
        public const string Hidden = "-";

        // ---- Effective (default tətbiq olunmuş) dəyərlər ----
        [JsonIgnore] public string EffectiveDescription => Or(Description, DefaultDescription);
        [JsonIgnore] public string EffectiveBalanceLabel => Or(BalanceLabel, DefaultBalanceLabel);
        [JsonIgnore] public string EffectiveCustomerLabel => Or(CustomerLabel, DefaultCustomerLabel);
        [JsonIgnore] public string EffectiveCashbackLabel => Or(CashbackLabel, DefaultCashbackLabel);
        [JsonIgnore] public string EffectiveAboutTitle => Or(AboutTitle, DefaultAboutTitle);
        [JsonIgnore] public string EffectiveAboutText => Or(AboutText, DefaultAboutText);
        [JsonIgnore] public string EffectiveWebsiteUrl => Or(WebsiteUrl, DefaultWebsiteUrl);
        [JsonIgnore] public string EffectiveWebsiteLabel => Or(WebsiteLabel, DefaultWebsiteLabel);
        [JsonIgnore] public bool NotifyOnBalanceChange => BalanceNotify == true;
        /// <summary>"%@" yoxdursa sona əlavə olunur (Apple changeMessage dəyəri yalnız placeholder ilə göstərir).</summary>
        [JsonIgnore] public string EffectiveBalanceChangeText
        {
            get
            {
                var t = Or(BalanceChangeText, DefaultBalanceChangeText);
                return t.Contains(ChangeValuePlaceholder) ? t : $"{t} {ChangeValuePlaceholder}";
            }
        }
        [JsonIgnore] public bool ShowAbout => AboutText?.Trim() != Hidden;
        [JsonIgnore] public bool ShowWebsite => WebsiteUrl?.Trim() != Hidden;

        private static string Or(string? v, string d) => string.IsNullOrWhiteSpace(v) ? d : v.Trim();

        /// <summary>DB JSON → model. Boş/yanlış JSON → defaults (heç vaxt throw etmir).</summary>
        public static CardDesignSettings Parse(string? json)
        {
            if (string.IsNullOrWhiteSpace(json)) return new CardDesignSettings();
            try
            {
                return JsonSerializer.Deserialize<CardDesignSettings>(json, JsonOptions) ?? new CardDesignSettings();
            }
            catch
            {
                return new CardDesignSettings();
            }
        }

        public string ToJson()
        {
            Version = CurrentVersion;
            return JsonSerializer.Serialize(this, JsonOptions);
        }

        /// <summary>Admin-dən gələn sahələri mövcud ayarların üstünə yaz (null = toxunma, "" = sil).</summary>
        public void Apply(string? title, string? logoText, string? description, string? issuerName,
            string? balanceLabel, string? customerLabel, string? cashbackLabel,
            string? aboutTitle, string? aboutText, string? websiteUrl, string? websiteLabel)
        {
            Title = Merge(Title, title);
            LogoText = Merge(LogoText, logoText);
            Description = Merge(Description, description);
            IssuerName = Merge(IssuerName, issuerName);
            BalanceLabel = Merge(BalanceLabel, balanceLabel);
            CustomerLabel = Merge(CustomerLabel, customerLabel);
            CashbackLabel = Merge(CashbackLabel, cashbackLabel);
            AboutTitle = Merge(AboutTitle, aboutTitle);
            AboutText = Merge(AboutText, aboutText);
            WebsiteUrl = Merge(WebsiteUrl, websiteUrl);
            WebsiteLabel = Merge(WebsiteLabel, websiteLabel);
        }

        /// <summary>Balans vahidi/formatı/bildirişi (null = toxunma; vahid/mətn "" = default; format 0 = Auto).</summary>
        public void ApplyBalance(string? unit, int? format, bool? notify, string? changeText)
        {
            BalanceUnit = Merge(BalanceUnit, unit);
            if (format.HasValue) BalanceFormat = format.Value == 0 ? null : (Main.BalanceFormat)format.Value;
            if (notify.HasValue) BalanceNotify = notify.Value ? true : null;
            BalanceChangeText = Merge(BalanceChangeText, changeText);
        }

        /// <summary>Admin layout JSON-u ({barcode, googleBanner, fields:[{id,slot,order,label}]}) — null sahə = toxunma.</summary>
        public void ApplyLayout(CardLayoutUpdateDto layout)
        {
            if (layout.Barcode.HasValue) Barcode = layout.Barcode.Value == 0 ? null : (DataAccess.Enums.BarcodeType)layout.Barcode.Value;
            if (layout.GoogleBanner.HasValue) GoogleBanner = layout.GoogleBanner.Value < 0 ? null : (Helpers.GoogleBannerPosition)layout.GoogleBanner.Value;
            if (layout.Fields != null)
                Fields = layout.Fields.Count == 0 ? null : layout.Fields
                    .Where(f => !string.IsNullOrWhiteSpace(f.Id))
                    .Select(f => new Helpers.CardFieldSetting { Id = f.Id.Trim(), Slot = (Helpers.CardFieldSlot)f.Slot, Order = f.Order, Label = string.IsNullOrWhiteSpace(f.Label) ? null : f.Label.Trim() })
                    .ToList();
        }

        private static string? Merge(string? current, string? incoming)
        {
            if (incoming == null) return current;          // sahə göndərilməyib → toxunma
            var t = incoming.Trim();
            return t.Length == 0 ? null : t;                 // "" → sil (default-a qayıt)
        }
    }
}

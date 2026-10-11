using System.Globalization;
using System.Text.Json;
using Loyalty.Business.Services.Abstractions.Main;

namespace Loyalty.Business.Helpers
{
    /// <summary>Tenant admin üçün oxunaqlı iiko proqram xülasəsi (bir org).</summary>
    public sealed class IikoProgramSummary
    {
        public string Id { get; set; } = string.Empty;
        public string? Name { get; set; }
        public string? Description { get; set; }
        public bool IsActive { get; set; }
        public string? WalletId { get; set; }
        public DateTime? ServiceFrom { get; set; }
        public DateTime? ServiceTo { get; set; }
        public List<IikoCampaignSummary> Campaigns { get; set; } = new();
    }

    public sealed class IikoCampaignSummary
    {
        public string Id { get; set; } = string.Empty;
        public string? Name { get; set; }
        public string? Description { get; set; }
        public bool IsActive { get; set; }
        public DateTime? PeriodFrom { get; set; }
        public DateTime? PeriodTo { get; set; }
        /// <summary>cashback_percent | fixed_bonus | payment | guest_category | other</summary>
        public string Kind { get; set; } = IikoRuleParser.KindOther;
        /// <summary>Oxunaqlı növ ("Keşbek %", "Balansla ödəniş" …).</summary>
        public string KindLabel { get; set; } = string.Empty;
        public decimal? Percent { get; set; }
        public decimal? Sum { get; set; }
        /// <summary>"Bütün məhsullar" / "Seçilmiş məhsullar" / "Məhsul filtri yoxdur".</summary>
        public string ProductScope { get; set; } = string.Empty;
        public bool AllProducts { get; set; }
        public List<IikoProductMask> ProductMasks { get; set; } = new();
        public List<IikoConditionSummary> Conditions { get; set; } = new();
        /// <summary>Orijinal action/condition-lar ("Xam qaydalar" bölməsi).</summary>
        public List<IikoRawRule> RawActions { get; set; } = new();
        public List<IikoRawRule> RawConditions { get; set; } = new();
    }

    public sealed record IikoProductMask(string Value, int? Type, string? ResolvedName);
    public sealed record IikoConditionSummary(string Type, string Text);
    public sealed record IikoRawRule(string Type, string? Settings);

    /// <summary>
    /// iiko marketinq kampaniyalarını (action/condition `settings` JSON mətnləri) oxunaqlı xülasəyə çevirir. Pure, test olunur.
    /// Yalnız sifariş (order) binding-ləri əsas götürülür; periodic / guest-registration / overdraft binding-ləri
    /// xülasədə görünmür, xam bölmədə qalır.
    /// </summary>
    public static class IikoRuleParser
    {
        public const string KindCashbackPercent = "cashback_percent";
        public const string KindFixedBonus = "fixed_bonus";
        public const string KindPayment = "payment";
        public const string KindGuestCategory = "guest_category";
        public const string KindOther = "other";

        public const string AllProductsText = "Bütün məhsullar";

        public static IikoProgramSummary Summarize(IikoLoyaltyProgramDetail program, Func<string, string?>? resolveName = null)
        {
            var summary = new IikoProgramSummary
            {
                Id = program.Id.ToString(),
                Name = program.Name,
                Description = program.Description,
                IsActive = program.IsActive,
                WalletId = program.WalletId?.ToString(),
                ServiceFrom = program.ServiceFrom,
                ServiceTo = program.ServiceTo
            };
            foreach (var mc in program.MarketingCampaigns ?? new())
                summary.Campaigns.Add(SummarizeCampaign(mc, resolveName));
            return summary;
        }

        public static IikoCampaignSummary SummarizeCampaign(IikoMarketingCampaign mc, Func<string, string?>? resolveName = null)
        {
            var c = new IikoCampaignSummary
            {
                Id = mc.Id.ToString(),
                Name = mc.Name,
                Description = mc.Description,
                IsActive = mc.IsActive,
                PeriodFrom = mc.PeriodFrom,
                PeriodTo = mc.PeriodTo
            };

            var orderBindings = mc.OrderActionConditionBindings ?? new();
            var masks = new List<IikoProductMask>();
            var hasMaskSource = false; // action/condition-da MaskItems sahəsi ümumiyyətlə var?

            foreach (var b in orderBindings)
            {
                foreach (var a in b.Actions ?? new())
                {
                    var type = Short(a.TypeName);
                    c.RawActions.Add(new IikoRawRule(type, a.Settings));
                    var s = Parse(a.Settings);

                    switch (type)
                    {
                        case "RefillWalletPercentAction":
                            SetKind(c, KindCashbackPercent, "Keşbek %");
                            c.Percent ??= Dec(s, "RefillPercent");
                            hasMaskSource |= CollectMasks(s, masks, resolveName);
                            break;
                        case "RefillWalletAction":
                            SetKind(c, KindFixedBonus, "Sabit bonus");
                            c.Sum ??= Dec(s, "RefillSum") ?? Dec(s, "Sum");
                            hasMaskSource |= CollectMasks(s, masks, resolveName);
                            break;
                        case "PaymentAction":
                            SetKind(c, KindPayment, "Balansla ödəniş");
                            c.Percent ??= Dec(s, "PaymentLimitPercent");
                            hasMaskSource |= CollectMasks(s, masks, resolveName);
                            break;
                        case "SetGuestCategoryAction":
                        case "RemoveGuestCategoryAction":
                            SetKind(c, KindGuestCategory, "Qonaq kateqoriyası");
                            break;
                        default:
                            SetKind(c, KindOther, Humanize(type));
                            break;
                    }
                }

                foreach (var cond in b.Conditions ?? new())
                {
                    var type = Short(cond.TypeName);
                    c.RawConditions.Add(new IikoRawRule(type, cond.Settings));
                    var s = Parse(cond.Settings);
                    if (type == "ProductInOrderCondition")
                    {
                        hasMaskSource |= CollectMasks(s, masks, resolveName);
                        var count = Int(s, "ItemsCount");
                        if (count.HasValue && count.Value > 1)
                            c.Conditions.Add(new IikoConditionSummary(type, $"Sifarişdə ən azı {count} məhsul"));
                        continue;
                    }
                    var text = DescribeCondition(type, s, resolveName);
                    if (text != null) c.Conditions.Add(new IikoConditionSummary(type, text));
                }
            }

            // Xam bölmə üçün digər binding növləri də (periodic / guest registration / overdraft)
            AddRaw(c, "periodic", mc.PeriodicActionConditionBindings);
            AddRaw(c, "guestRegistration", mc.GuestRegistrationActionConditionBindings);
            AddRaw(c, "overdraft", mc.OverdraftActionConditionBindings);

            if (string.IsNullOrEmpty(c.KindLabel))
            {
                c.Kind = KindOther;
                c.KindLabel = orderBindings.Count == 0 ? "Sifariş qaydası yoxdur" : "Digər";
            }

            // Məhsul əhatəsi
            var all = masks.Any(m => m.Value == "*");
            var concrete = masks.Where(m => m.Value != "*")
                .GroupBy(m => m.Value, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.First())
                .ToList();
            c.AllProducts = all || (hasMaskSource && concrete.Count == 0);
            c.ProductMasks = concrete;
            c.ProductScope = c.AllProducts
                ? AllProductsText
                : concrete.Count > 0 ? "Seçilmiş məhsullar" : "Məhsul filtri yoxdur";

            return c;
        }

        /// <summary>Proqramın effektiv keşbek faizi: şərtsiz aktiv "Keşbek %" kampaniyası, yoxdursa ilk aktiv.</summary>
        public static decimal? ProgramCashbackPercent(IikoProgramSummary p)
            => p.Campaigns.FirstOrDefault(x => x.IsActive && x.Kind == KindCashbackPercent && x.Percent.HasValue && x.Conditions.Count == 0)?.Percent
               ?? p.Campaigns.FirstOrDefault(x => x.IsActive && x.Kind == KindCashbackPercent && x.Percent.HasValue)?.Percent;

        /// <summary>"LoyaltyProgramEngine.Actions.Payment.OverdraftAction" → "OverdraftAction".</summary>
        public static string Short(string? typeName)
        {
            if (string.IsNullOrEmpty(typeName)) return "?";
            var i = typeName.LastIndexOf('.');
            return i >= 0 ? typeName[(i + 1)..] : typeName;
        }

        // ── helpers ──
        private static void AddRaw(IikoCampaignSummary c, string label, List<IikoActionConditionBinding>? list)
        {
            foreach (var b in list ?? new())
            {
                foreach (var a in b.Actions ?? new())
                    c.RawActions.Add(new IikoRawRule($"{label}:{Short(a.TypeName)}", a.Settings));
                foreach (var cond in b.Conditions ?? new())
                    c.RawConditions.Add(new IikoRawRule($"{label}:{Short(cond.TypeName)}", cond.Settings));
            }
        }

        private static void SetKind(IikoCampaignSummary c, string kind, string label)
        {
            // İlk mənalı action növü qalib gəlir; "other" yalnız boşdursa
            if (string.IsNullOrEmpty(c.KindLabel) || (c.Kind == KindOther && kind != KindOther))
            {
                c.Kind = kind;
                c.KindLabel = label;
            }
        }

        private static bool CollectMasks(JsonElement? s, List<IikoProductMask> into, Func<string, string?>? resolve)
        {
            if (s == null || !s.Value.TryGetProperty("MaskItems", out var arr) || arr.ValueKind != JsonValueKind.Array) return false;
            foreach (var m in arr.EnumerateArray())
            {
                if (m.ValueKind != JsonValueKind.Object) continue;
                var value = m.TryGetProperty("MaskValue", out var mv) && mv.ValueKind == JsonValueKind.String ? mv.GetString() : null;
                if (string.IsNullOrWhiteSpace(value)) continue;
                int? type = m.TryGetProperty("MaskType", out var mt) && mt.ValueKind == JsonValueKind.Number ? mt.GetInt32() : null;
                var name = value == "*" ? null : resolve?.Invoke(value);
                into.Add(new IikoProductMask(value, type, name));
            }
            return true;
        }

        private static string? DescribeCondition(string type, JsonElement? s, Func<string, string?>? resolve)
        {
            switch (type)
            {
                case "GuestCategoryCondition":
                    return "Qonaq kateqoriyası: " + Ids(s, "GuestCategoryIds", resolve);
                case "GuestNotInCategoryCondition":
                    return "Qonaq kateqoriyasında DEYİL: " + Ids(s, "GuestCategoryIds", resolve);
                case "OrdersNumberCondition":
                    {
                        var n = Int(s, "OrdersNumber");
                        var period = Int(s, "Period");
                        var text = "Sifariş sayı: " + (n?.ToString(CultureInfo.InvariantCulture) ?? "?");
                        return period is > 0 ? text + $" (dövr {period})" : text;
                    }
                case "TimetableCondition":
                    return "Vaxt cədvəli";
                case "GuestRegistrationSourceCondition":
                    return "Qeydiyyat mənbəyi: " + Ids(s, "AcceptedSources", null);
                case "OrderSumCondition":
                case "OrderTotalCondition":
                    {
                        var min = Dec(s, "MinSum") ?? Dec(s, "Sum") ?? Dec(s, "OrderSum");
                        return min.HasValue
                            ? "Sifariş məbləği ≥ " + min.Value.ToString(CultureInfo.InvariantCulture)
                            : "Sifariş məbləği şərti";
                    }
                default:
                    return Humanize(type);
            }
        }

        private static string Ids(JsonElement? s, string prop, Func<string, string?>? resolve)
        {
            if (s == null || !s.Value.TryGetProperty(prop, out var arr) || arr.ValueKind != JsonValueKind.Array) return "—";
            var parts = new List<string>();
            foreach (var e in arr.EnumerateArray())
            {
                var raw = e.ValueKind == JsonValueKind.String ? e.GetString() : e.ToString();
                if (string.IsNullOrEmpty(raw)) continue;
                parts.Add(resolve?.Invoke(raw) ?? raw);
            }
            return parts.Count == 0 ? "—" : string.Join(", ", parts);
        }

        private static JsonElement? Parse(string? settings)
        {
            if (string.IsNullOrWhiteSpace(settings)) return null;
            try
            {
                using var doc = JsonDocument.Parse(settings);
                return doc.RootElement.Clone();
            }
            catch (JsonException) { return null; }
        }

        private static decimal? Dec(JsonElement? s, string prop)
        {
            if (s == null || !s.Value.TryGetProperty(prop, out var v)) return null;
            return v.ValueKind == JsonValueKind.Number && v.TryGetDecimal(out var d) ? d : null;
        }

        private static int? Int(JsonElement? s, string prop)
        {
            if (s == null || !s.Value.TryGetProperty(prop, out var v)) return null;
            return v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out var i) ? i : null;
        }

        private static string Humanize(string type)
        {
            // "SomeFancyAction" → "Some Fancy Action"
            var sb = new System.Text.StringBuilder();
            foreach (var ch in type)
            {
                if (char.IsUpper(ch) && sb.Length > 0) sb.Append(' ');
                sb.Append(ch);
            }
            return sb.ToString();
        }
    }
}

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

        // ── Strukturlu çıxarış (bütün binding-lər: order + periodic + guestRegistration + overdraft) — idxal/seçim üçün ──
        /// <summary>Action settings-dəki cüzdan id-ləri (RefillWalletPercent/Refill/Payment …), təkrarsız.</summary>
        public List<string> WalletIds { get; set; } = new();
        /// <summary>PaymentAction (cüzdandan ödəniş) — məs. pims ambassador: F&amp;B kateqoriyası, 1 məhsul = 1 vahid.</summary>
        public IikoPaymentSpec? Payment { get; set; }
        /// <summary>GuestCategoryCondition — kampaniya yalnız bu qonaq kateqoriyalarına işləyir.</summary>
        public List<string> GuestCategoryIds { get; set; } = new();
        /// <summary>GuestNotInCategoryCondition — bu kateqoriyalar istisnadır.</summary>
        public List<string> ExcludedGuestCategoryIds { get; set; } = new();
        /// <summary>PresentBonusAction — "N al → 1 pulsuz" hədiyyəsi (cüzdandan vahid silinir).</summary>
        public IikoGiftSpec? Gift { get; set; }
        /// <summary>AdditionalBonusFixedSumAction — sabit bonus (Multiplicity ilə hər uyğun məhsula).</summary>
        public IikoFixedEarnSpec? FixedEarn { get; set; }
    }

    /// <summary>PaymentAction: hansı cüzdandan, hansı məhsullara, məhsul başına neçə vahid (UnitsPrice; null = məbləğlə).</summary>
    public sealed record IikoPaymentSpec(string? WalletId, decimal? UnitsPerItem, int? WhatToPay, decimal? LimitPercent, List<IikoProductMask> Masks);

    /// <summary>
    /// PresentBonusAction: hədiyyə başına cüzdandan silinən vahid (WithdrawalMultiplier), bir istifadədə neçə hədiyyə (action sayı),
    /// hədiyyə ola bilən məhsul kodları; "*" + ölçülər = həmin ölçülü istənilən məhsul (sifarişdə ölçü gəlmir — endirim növü ilə tanınır).
    /// </summary>
    public sealed record IikoGiftSpec(
        decimal CostPerGift, int GiftsPerUse, bool WithdrawFromWallet, List<string> ProductCodes, bool AnySizedProduct,
        string? Caption, string? WalletId, decimal? MinBalance);

    /// <summary>AdditionalBonusFixedSumAction + ProductInOrderCondition: uyğun məhsula (PerItem = hər ədədə) BonusPerItem vahid.</summary>
    public sealed record IikoFixedEarnSpec(decimal BonusPerItem, bool PerItem, string? WalletId, List<IikoProductMask> Masks);

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
        public const string KindGift = "gift";
        public const string KindOther = "other";

        /// <summary>iiko MaskType: 1 = məhsul kodu, 3 = kateqoriya adı (";" ilə bir neçə).</summary>
        public const int MaskTypeProduct = 1;
        public const int MaskTypeCategory = 3;

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
                // Binding səviyyəli hədiyyə / sabit bonus xammalı (action + onun şərtləri eyni binding-dədir)
                var giftActions = new List<JsonElement?>();
                JsonElement? fixedBonus = null;
                var conditionMasks = new List<IikoProductMask>();
                var multiplicity = false;
                string? caption = null;
                string? balanceWallet = null;
                decimal? minBalance = null;

                foreach (var a in b.Actions ?? new())
                {
                    var type = Short(a.TypeName);
                    c.RawActions.Add(new IikoRawRule(type, a.Settings));
                    var s = Parse(a.Settings);

                    switch (type)
                    {
                        case "PresentBonusAction":
                            SetKind(c, KindGift, "Hədiyyə");
                            giftActions.Add(s);
                            break;
                        case "AdditionalBonusFixedSumAction":
                            SetKind(c, KindFixedBonus, "Sabit bonus");
                            c.Sum ??= Dec(s, "BonusValue");
                            fixedBonus ??= s;
                            break;
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
                        CollectMasks(s, conditionMasks, resolveName);
                        multiplicity |= Bool(s, "MultiplicityEnabled") == true;
                        var count = Int(s, "ItemsCount");
                        if (count.HasValue && count.Value > 1)
                            c.Conditions.Add(new IikoConditionSummary(type, $"Sifarişdə ən azı {count} məhsul"));
                        continue;
                    }
                    if (type == "ManualCondition") caption ??= Str(s, "Caption");
                    if (type == "WalletBalanceCondition")
                    {
                        balanceWallet ??= Str(s, "WalletId");
                        minBalance ??= Dec(s, "Sum");
                    }
                    var text = DescribeCondition(type, s, resolveName);
                    if (text != null) c.Conditions.Add(new IikoConditionSummary(type, text));
                }

                if (giftActions.Count > 0 && c.Gift == null)
                    c.Gift = BuildGift(giftActions, caption, balanceWallet, minBalance);
                if (fixedBonus != null && c.FixedEarn == null && Dec(fixedBonus, "BonusValue") is decimal bonus && bonus > 0)
                {
                    c.FixedEarn = new IikoFixedEarnSpec(bonus, multiplicity, Str(fixedBonus, "WalletId"), SplitCategoryMasks(conditionMasks));
                    if (multiplicity && c.Kind == KindFixedBonus) c.KindLabel =$"Hər məhsula +{bonus.ToString("0.##", CultureInfo.InvariantCulture)}";
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

            ExtractStructured(c, resolveName);
            return c;
        }

        /// <summary>
        /// Xam action/condition-lardan (bütün binding-lər, "overdraft:" kimi etiketlər daxil) cüzdan id-ləri, PaymentAction
        /// və qonaq kateqoriyası şərtlərini strukturlu çıxarır.
        /// </summary>
        private static void ExtractStructured(IikoCampaignSummary c, Func<string, string?>? resolveName)
        {
            static string BareType(string t) { var i = t.IndexOf(':'); return i >= 0 ? t[(i + 1)..] : t; }

            foreach (var a in c.RawActions)
            {
                var s = Parse(a.Settings);
                var walletId = Str(s, "WalletId");
                if (!string.IsNullOrWhiteSpace(walletId) && !c.WalletIds.Contains(walletId, StringComparer.OrdinalIgnoreCase))
                    c.WalletIds.Add(walletId);

                if (BareType(a.Type) == "PaymentAction" && c.Payment == null)
                {
                    var masks = new List<IikoProductMask>();
                    CollectMasks(s, masks, resolveName);
                    c.Payment = new IikoPaymentSpec(
                        WalletId: walletId,
                        UnitsPerItem: FirstMaskDecimal(s, "UnitsPrice"),
                        WhatToPay: Int(s, "WhatToPay"),
                        LimitPercent: Dec(s, "PaymentLimitPercent"),
                        Masks: masks);
                }
            }

            foreach (var cond in c.RawConditions)
            {
                var type = BareType(cond.Type);
                var target = type switch
                {
                    "GuestCategoryCondition" => c.GuestCategoryIds,
                    "GuestNotInCategoryCondition" => c.ExcludedGuestCategoryIds,
                    _ => null
                };
                if (target == null) continue;
                foreach (var id in IdList(Parse(cond.Settings), "GuestCategoryIds"))
                    if (!target.Contains(id, StringComparer.OrdinalIgnoreCase)) target.Add(id);
            }
        }

        /// <summary>Bir binding-in PresentBonusAction-ları: hər action bir hədiyyədir (İki pulsuz içki = 2 action).</summary>
        private static IikoGiftSpec BuildGift(List<JsonElement?> actions, string? caption, string? balanceWallet, decimal? minBalance)
        {
            var codes = new List<string>();
            var anySized = false;
            decimal? cost = null;
            var withdraw = false;
            foreach (var s in actions)
            {
                cost ??= Dec(s, "WithdrawalMultiplier");
                withdraw |= Bool(s, "WithdrawFromWalletOnUse") == true;
                if (s == null || !s.Value.TryGetProperty("Items", out var items) || items.ValueKind != JsonValueKind.Array) continue;
                foreach (var it in items.EnumerateArray())
                {
                    if (it.ValueKind != JsonValueKind.Object) continue;
                    var code = it.TryGetProperty("Code", out var cv) && cv.ValueKind == JsonValueKind.String ? cv.GetString()?.Trim() : null;
                    if (string.IsNullOrEmpty(code)) continue;
                    if (code == "*") { anySized = true; continue; }
                    if (!codes.Contains(code, StringComparer.OrdinalIgnoreCase)) codes.Add(code);
                }
            }
            return new IikoGiftSpec(cost ?? 0m, actions.Count, withdraw, codes, anySized,
                string.IsNullOrWhiteSpace(caption) ? null : caption.Trim(), balanceWallet, minBalance);
        }

        /// <summary>Kateqoriya maskası "HOT BAR;COLD BAR" → iki ayrı maska; təkrarlar atılır.</summary>
        private static List<IikoProductMask> SplitCategoryMasks(List<IikoProductMask> masks)
        {
            var result = new List<IikoProductMask>();
            foreach (var m in masks)
            {
                var parts = m.Type == MaskTypeCategory
                    ? m.Value.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    : new[] { m.Value.Trim() };
                foreach (var p in parts)
                    if (!result.Any(x => x.Type == m.Type && string.Equals(x.Value, p, StringComparison.OrdinalIgnoreCase)))
                        result.Add(new IikoProductMask(p, m.Type, parts.Length == 1 ? m.ResolvedName : null));
            }
            return result;
        }

        private static bool? Bool(JsonElement? s, string prop)
            => s != null && s.Value.TryGetProperty(prop, out var v) && v.ValueKind is JsonValueKind.True or JsonValueKind.False ? v.GetBoolean() : null;

        private static string? Str(JsonElement? s, string prop)
            => s != null && s.Value.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

        private static decimal? FirstMaskDecimal(JsonElement? s, string prop)
        {
            if (s == null || !s.Value.TryGetProperty("MaskItems", out var arr) || arr.ValueKind != JsonValueKind.Array) return null;
            foreach (var m in arr.EnumerateArray())
                if (m.ValueKind == JsonValueKind.Object && m.TryGetProperty(prop, out var v)
                    && v.ValueKind == JsonValueKind.Number && v.TryGetDecimal(out var d))
                    return d;
            return null;
        }

        private static IEnumerable<string> IdList(JsonElement? s, string prop)
        {
            if (s == null || !s.Value.TryGetProperty(prop, out var arr) || arr.ValueKind != JsonValueKind.Array) yield break;
            foreach (var e in arr.EnumerateArray())
            {
                var raw = e.ValueKind == JsonValueKind.String ? e.GetString() : e.ToString();
                if (!string.IsNullOrWhiteSpace(raw)) yield return raw!;
            }
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
                case "ManualCondition":
                    {
                        var caption = Str(s, "Caption");
                        return string.IsNullOrWhiteSpace(caption) ? "Kassir əl ilə seçir" : $"Kassir əl ilə seçir: «{caption.Trim()}»";
                    }
                case "WalletBalanceCondition":
                    {
                        var sum = Dec(s, "Sum");
                        // Operation 4 = "≥" (real Cupcup: 7 vahiddə pulsuz desert); digərləri ümumi mətn
                        return sum.HasValue
                            ? (Int(s, "Operation") == 4 ? "Cüzdan balansı ≥ " : "Cüzdan balansı şərti: ") + sum.Value.ToString("0.##", CultureInfo.InvariantCulture)
                            : "Cüzdan balansı şərti";
                    }
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

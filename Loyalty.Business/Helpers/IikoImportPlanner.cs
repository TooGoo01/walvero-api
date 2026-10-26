using Loyalty.Business.Services.Abstractions.Main;
using Loyalty.DataAccess.Entities.Main;
using Loyalty.DataAccess.Enums;

namespace Loyalty.Business.Helpers
{
    /// <summary>Walvero tərəfinin planlayıcı üçün qısa vəziyyəti (DB-dən doldurulur).</summary>
    public sealed class WalveroPosState
    {
        public int TenantId { get; set; }
        public bool AmbassadorsEnabled { get; set; }
        /// <summary>İnteqrasiyanın default proqramı (TenantIntegration.LoyaltyProgramId).</summary>
        public int? IntegrationProgramId { get; set; }
        public string? AmbassadorCategoryId { get; set; }
        public List<WalveroProgramState> Programs { get; set; } = new();
        public int EarnRuleCount { get; set; }
        public int ExclusionCount { get; set; }

        // ── Hədiyyə idxalı üçün ──
        public List<WalveroEarnRuleState> EarnRules { get; set; } = new();
        public List<WalveroRewardState> Rewards { get; set; } = new();
        /// <summary>Sifarişlərdə görünmüş kassa endirimləri (hədiyyə endiriminin adla təklifi).</summary>
        public List<IikoDiscountOptionDto> ObservedDiscounts { get; set; } = new();
        /// <summary>Kassa kateqoriyası adı → ID (nomenklatura + sifarişlərdən), böyük/kiçik hərf fərqsiz.</summary>
        public Dictionary<string, string> CategoryIdByName { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    }

    public sealed record WalveroEarnRuleState(int? ProgramId, PosEarnExclusionKind Kind, string ExternalId, string? Code);

    public sealed class WalveroRewardState
    {
        public int Id { get; set; }
        public int ProgramId { get; set; }
        public string Name { get; set; } = string.Empty;
        public int PointsCost { get; set; }
        public string? PosSourceKey { get; set; }
        public string? PosDiscountTypeId { get; set; }
        public List<string> ProductCodes { get; set; } = new();
    }

    public sealed class WalveroProgramState
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public ProgramAudience Audience { get; set; }
        public CardTemplateType? TemplateType { get; set; }
        public PosEarnSource PosEarnSource { get; set; }
        public string? PosWalletId { get; set; }
        public string? PosExternalProgramId { get; set; }
        public int? BronzePercent { get; set; }
        public string? Currency { get; set; }
        /// <summary>Möhür kartında hədiyyəyə qədər addım sayı.</summary>
        public int? TotalSteps { get; set; }
    }

    /// <summary>
    /// Kassa (iiko) kampaniyalarından Walvero ayar təklifləri (bir kliklə idxal) və sinxron vəziyyəti. Pure — test olunur.
    /// Təkliflər idempotentdir: artıq tətbiq olunmuş dəyişiklik təklif edilmir (Changes boşdursa "artıq uyğundur").
    /// </summary>
    public static class IikoImportPlanner
    {
        public const string KindCashback = "cashback";
        public const string KindUnitPayment = "unit_payment";
        public const string KindInfo = "info";
        public const string KindUnsupported = "unsupported";
        public const string KindGiftEarn = "gift_earn";
        public const string KindGiftReward = "gift_reward";

        public const string TargetProgram = "program";
        public const string TargetNewAmbassadorProgram = "new_ambassador_program";
        public const string TargetTemplate = "template";
        public const string TargetIntegration = "integration";
        public const string TargetEarnRule = "earn_rule";
        public const string TargetReward = "reward";

        /// <summary>earn_rule dəyişikliyinin Field-i = qayda növü.</summary>
        public const string FieldCategory = "Category";
        public const string FieldProduct = "Product";
        /// <summary>reward dəyişikliklərinin Field-ləri (bir təklifin reward dəyişiklikləri bir hədiyyəni təsvir edir).</summary>
        public const string FieldRewardId = "RewardId";
        public const string FieldName = "Name";
        public const string FieldPointsCost = "PointsCost";
        public const string FieldProducts = "Products";
        public const string FieldDiscountType = "PosDiscountTypeId";
        public const string FieldSourceKey = "PosSourceKey";

        public const string UnitCurrency = "məhsul";

        /// <summary>Keşbek variantları: kassa hesablayır (güzgü) / Walvero hesablayır.</summary>
        public const string VariantPos = "pos";
        public const string VariantWalvero = "walvero";

        public static List<IikoImportProposalDto> Plan(IReadOnlyList<IikoCatalogProgramDto> iikoPrograms, WalveroPosState w)
        {
            var proposals = new List<IikoImportProposalDto>();
            var publicTarget = w.Programs.FirstOrDefault(p => p.Id == w.IntegrationProgramId && p.Audience != ProgramAudience.Ambassador)
                               ?? w.Programs.FirstOrDefault(p => p.Audience != ProgramAudience.Ambassador);
            var ambassador = w.Programs.FirstOrDefault(p => p.Audience == ProgramAudience.Ambassador);
            var claimedPrograms = new HashSet<int>();
            var ambassadorPlanned = false;

            foreach (var ip in iikoPrograms.Where(p => p.IsActive))
            {
                // 0) "N al → 1 pulsuz": hədiyyə kampaniyaları + eyni proqramın sabit bonus (qazanc) kampaniyaları dəst kimi
                var giftCampaigns = ip.Campaigns.Where(c => c.IsActive && c.Gift is { CostPerGift: > 0 }).ToList();
                if (giftCampaigns.Count > 0)
                    proposals.AddRange(PlanGiftSet(ip, giftCampaigns, publicTarget, w));

                foreach (var c in ip.Campaigns.Where(c => c.IsActive))
                {
                    if (giftCampaigns.Count > 0 && (c.Gift != null || c.FixedEarn != null)) continue;

                    var walletId = c.Payment?.WalletId ?? c.WalletIds.FirstOrDefault() ?? ip.WalletId;
                    var label = $"{ip.Name} → {c.Name}";

                    // 1) Ümumi keşbek — iki alternativ: kassa hesablasın (güzgü) və ya Walvero hesablasın (kassa kampaniyası söndürülür)
                    if (c.Kind == IikoRuleParser.KindCashbackPercent && c.AllProducts && c.GuestCategoryIds.Count == 0 && c.Percent.HasValue)
                    {
                        var group = $"{KindCashback}:{c.Id}";
                        var title = $"Keşbek {c.Percent:0.##}% — {label}";
                        var pos = new IikoImportProposalDto
                        {
                            Key = $"{group}:{VariantPos}",
                            Group = group,
                            Kind = KindCashback,
                            Title = $"{title} · kassa hesablayır",
                            Explanation = "Keşbeki kassanın öz kampaniyası hesablayır; Walvero kartı kassa cüzdanının balansını göstərir. Kassada dəyişiklik lazım deyil.",
                        };
                        var walvero = new IikoImportProposalDto
                        {
                            Key = $"{group}:{VariantWalvero}",
                            Group = group,
                            Kind = KindCashback,
                            Title = $"{title} · Walvero hesablayır",
                            Explanation = $"Walvero hər sifarişdən {c.Percent:0.##}% hesablayır və balansı kassa cüzdanına göndərir; kassanın cüzdan əməliyyatları kartın balansını əvəz etmir.",
                        };
                        string? blocked = publicTarget == null
                            ? "Adi (Public) Walvero proqramı yoxdur."
                            : !claimedPrograms.Add(publicTarget.Id)
                                ? $"«{publicTarget.Name}» artıq başqa keşbek kampaniyasına bağlanır — yalnız biri seçilə bilər."
                                : null;
                        if (blocked != null)
                        {
                            pos.Warning = walvero.Warning = blocked;
                        }
                        else
                        {
                            FillCashback(pos, publicTarget!, PosEarnSource.PosMirror, walletId, ip.Id, c.Percent.Value);
                            FillCashback(walvero, publicTarget!, PosEarnSource.WalveroRules, walletId, ip.Id, c.Percent.Value);
                            Finish(pos);
                            walvero.Warning = $"Kassada «{c.Name}» kampaniyasını söndürün — yoxsa kassa cüzdanına {c.Percent:0.##}% iki dəfə yazılır (kassa özü + Walvero).";
                            Finish(walvero);
                            // Qazanc rejimi şüurlu seçimdir — heç bir variant default seçilmir (köhnə "hər ikisi" rejimi də qanunidir)
                            pos.SelectedByDefault = walvero.SelectedByDefault = false;
                        }
                        proposals.Add(pos);
                        proposals.Add(walvero);
                        continue;
                    }

                    // 2) Cüzdandan say ilə ödəniş (məhsul başına vahid) — ambassador proqramı
                    if (c.Payment != null && c.Payment.UnitsPerItem.HasValue)
                    {
                        var guestCategory = c.GuestCategoryIds.FirstOrDefault();
                        var masks = string.Join(", ", c.Payment.Masks.Select(m => m.ResolvedName ?? m.Value));
                        var p = new IikoImportProposalDto
                        {
                            Key = $"unit_payment:{c.Id}",
                            Kind = KindUnitPayment,
                            Title = $"Say ilə ödəniş ({c.Payment.UnitsPerItem:0.##} vahid/məhsul{(masks.Length > 0 ? ", " + masks : "")}) — {label}",
                            Explanation = "Ambassador proqramı: balans = pulsuz məhsul sayı, kassa cüzdanından ödənilir; ambassador-lar kassada qonaq kateqoriyasına avtomatik yazılır.",
                        };
                        if (ambassadorPlanned)
                        {
                            p.Applicable = false;
                            p.Warning = "Ambassador proqramı artıq başqa təklifdə qurulur.";
                            proposals.Add(p);
                            continue;
                        }
                        if (guestCategory == null)
                            p.Warning = "Kampaniyada qonaq kateqoriyası şərti yoxdur — cüzdandan hər müştəri ödəyə bilər. Ambassador üçün olduğuna əmin olun.";
                        if (!w.AmbassadorsEnabled)
                            p.Warning = (p.Warning == null ? "" : p.Warning + " ") + "Tenant-da ambassador funksiyası söndürülüb — super admin açmalıdır.";

                        if (ambassador == null)
                        {
                            p.Changes.Add(new IikoImportChangeDto(TargetNewAmbassadorProgram, null, "Ambassador", "Yeni proqram", null,
                                $"Ambassador, xal kartı, valyuta «{UnitCurrency}», kassa hesablayır"));
                            p.Changes.Add(new IikoImportChangeDto(TargetNewAmbassadorProgram, null, "Ambassador", "PosWalletId", null, walletId));
                            p.Changes.Add(new IikoImportChangeDto(TargetNewAmbassadorProgram, null, "Ambassador", "PosExternalProgramId", null, ip.Id));
                        }
                        else
                        {
                            AddProgramChange(p, ambassador, "PosEarnSource", ambassador.PosEarnSource.ToString(), nameof(PosEarnSource.PosMirror));
                            AddProgramChange(p, ambassador, "PosWalletId", ambassador.PosWalletId, walletId);
                            AddProgramChange(p, ambassador, "PosExternalProgramId", ambassador.PosExternalProgramId, ip.Id);
                            if (ambassador.TemplateType == CardTemplateType.ProgressBased)
                                p.Warning = (p.Warning == null ? "" : p.Warning + " ") + "Mövcud ambassador proqramı möhür kartıdır — say balansı üçün xal kartı lazımdır (super admin şablonu dəyişməlidir).";
                            else if (!string.Equals(ambassador.Currency, UnitCurrency, StringComparison.OrdinalIgnoreCase))
                                p.Changes.Add(new IikoImportChangeDto(TargetTemplate, ambassador.Id, ambassador.Name, "Currency", ambassador.Currency, UnitCurrency));
                        }
                        if (guestCategory != null && !string.Equals(w.AmbassadorCategoryId, guestCategory, StringComparison.OrdinalIgnoreCase))
                            p.Changes.Add(new IikoImportChangeDto(TargetIntegration, null, "İnteqrasiya", "AmbassadorIikoCategoryId", w.AmbassadorCategoryId, guestCategory));

                        ambassadorPlanned = true;
                        Finish(p);
                        proposals.Add(p);
                        continue;
                    }

                    // 3) Məhsul-scoped keşbek — kassa özü hesablayır
                    if (c.Kind == IikoRuleParser.KindCashbackPercent)
                    {
                        proposals.Add(new IikoImportProposalDto
                        {
                            Key = $"info:{c.Id}",
                            Kind = KindInfo,
                            Title = $"{c.KindLabel}{(c.Percent.HasValue ? $" {c.Percent:0.##}%" : "")} — {label}",
                            Explanation = c.GuestCategoryIds.Count > 0
                                ? "Yalnız müəyyən qonaq kateqoriyasına işləyir — kassa özü hesablayır, Walvero-da əməliyyat lazım deyil."
                                : "Seçilmiş məhsullara işləyir — kassa özü hesablayır, Walvero-da əməliyyat lazım deyil.",
                            Applicable = false
                        });
                        continue;
                    }

                    // 4) Qarşılığı olmayanlar
                    proposals.Add(new IikoImportProposalDto
                    {
                        Key = $"unsupported:{c.Id}",
                        Kind = KindUnsupported,
                        Title = $"{c.KindLabel} — {label}",
                        Explanation = "Walvero-da qarşılığı yoxdur — kassada qalır, toxunulmur.",
                        Applicable = false
                    });
                }
            }
            return proposals;
        }

        /// <summary>
        /// Bir kassa proqramının hədiyyə dəsti: (a) sabit bonus kampaniyalarından qazanc qaydaları, (b) qiymət üzrə bir Walvero hədiyyəsi
        /// (eyni qiymətli "Bir/İki/Üç pulsuz" kampaniyaları birləşir — say sifarişdəki endirim sətirlərindən gəlir).
        /// Hədəf: inteqrasiyanın adi proqramı. Heç biri default seçilmir — admin hansı dəstin Walvero kartına aid olduğunu özü seçir.
        /// </summary>
        private static IEnumerable<IikoImportProposalDto> PlanGiftSet(
            IikoCatalogProgramDto ip, List<IikoCampaignSummary> giftCampaigns, WalveroProgramState? target, WalveroPosState w)
        {
            var programLabel = (ip.Name ?? ip.Id).Trim();
            var earnCampaigns = ip.Campaigns.Where(c => c.IsActive && c.Gift == null && c.FixedEarn != null).ToList();
            var targetNote = target == null ? null : $"Walvero proqramı: «{target.Name}».";

            // (a) Qazanc qaydaları
            if (earnCampaigns.Count > 0)
            {
                var bonus = earnCampaigns[0].FixedEarn!.BonusPerItem;
                var p = new IikoImportProposalDto
                {
                    Key = $"{KindGiftEarn}:{ip.Id}",
                    Kind = KindGiftEarn,
                    Title = $"Qazanc: hər məhsula +{bonus:0.##} — {programLabel}",
                    Explanation = $"Kassada vahid yazan məhsul/kateqoriyalar Walvero kartına da möhür (xal) yazsın. {targetNote}".Trim()
                };
                if (target == null)
                {
                    p.Warning = "Adi (Public) Walvero proqramı yoxdur.";
                }
                else
                {
                    var unresolved = new List<string>();
                    var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    foreach (var mask in earnCampaigns.SelectMany(c => c.FixedEarn!.Masks.Select(m => (Mask: m, c.FixedEarn.BonusPerItem))))
                    {
                        var m = mask.Mask;
                        if (m.Value == "*") { unresolved.Add("bütün məhsullar (*)"); continue; }
                        if (m.Type == IikoRuleParser.MaskTypeCategory)
                        {
                            if (!w.CategoryIdByName.TryGetValue(m.Value, out var catId)) { unresolved.Add($"kateqoriya «{m.Value}»"); continue; }
                            if (!seen.Add("c:" + catId)) continue;
                            if (!w.EarnRules.Any(r => r.Kind == PosEarnExclusionKind.Category && Same(r.ExternalId, catId) && AppliesTo(r, target)))
                                p.Changes.Add(new IikoImportChangeDto(TargetEarnRule, target.Id, m.Value, FieldCategory, null, catId, mask.BonusPerItem));
                        }
                        else if (m.Type == IikoRuleParser.MaskTypeProduct)
                        {
                            if (!seen.Add("p:" + m.Value)) continue;
                            if (!w.EarnRules.Any(r => r.Kind == PosEarnExclusionKind.Product && (Same(r.Code, m.Value) || Same(r.ExternalId, m.Value)) && AppliesTo(r, target)))
                                p.Changes.Add(new IikoImportChangeDto(TargetEarnRule, target.Id, m.ResolvedName ?? m.Value, FieldProduct, null, m.Value, mask.BonusPerItem));
                        }
                        else unresolved.Add($"«{m.Value}» (maska növü {m.Type})");
                    }
                    if (unresolved.Count > 0)
                        p.Warning = "Kassa kataloqunda tapılmadı, əl ilə əlavə edin: " + string.Join(", ", unresolved) + ".";
                }
                Finish(p);
                p.SelectedByDefault = false;
                yield return p;
            }

            // (b) Hədiyyə(lər) — qiymət üzrə
            foreach (var byCost in giftCampaigns.GroupBy(c => c.Gift!.CostPerGift).OrderBy(g => g.Key))
            {
                var cost = byCost.Key;
                var list = byCost.ToList();
                // Ad: tək hədiyyəli kampaniyanın kassir mətni ("Pulsuz 1 dessert", "Bir pulsuz içki")
                var single = list.FirstOrDefault(c => c.Gift!.GiftsPerUse == 1) ?? list[0];
                var name = (single.Gift!.Caption ?? single.Name ?? programLabel).Trim();
                var codes = list.SelectMany(c => c.Gift!.ProductCodes).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                var anySized = list.Any(c => c.Gift!.AnySizedProduct);
                var sourceKey = $"{ip.Id}:{cost.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)}";
                var costInt = (int)Math.Ceiling(cost);

                var p = new IikoImportProposalDto
                {
                    Key = $"{KindGiftReward}:{sourceKey}",
                    Kind = KindGiftReward,
                    Title = $"Hədiyyə: «{name}» — {cost:0.##} vahid, {(anySized ? "istənilən ölçülü məhsul" + (codes.Count > 0 ? $" + {codes.Count} məhsul" : "") : $"{codes.Count} məhsul")} — {programLabel}",
                    Explanation = "Kassada bu hədiyyə veriləndə (sifarişdə kassa endirimi görünəndə) Walvero kartından da silinir, tarixçədə hədiyyə adı görünür. "
                                  + (list.Count > 1 ? $"Birləşən kampaniyalar: {string.Join(", ", list.Select(c => c.Name))}. " : "")
                                  + targetNote,
                    DiscountTypeSelectable = true
                };
                if (target == null)
                {
                    p.Warning = "Adi (Public) Walvero proqramı yoxdur.";
                    Finish(p);
                    yield return p;
                    continue;
                }

                var existing = w.Rewards.FirstOrDefault(r => r.ProgramId == target.Id && Same(r.PosSourceKey, sourceKey));
                var matched = MatchDiscount(w.ObservedDiscounts, list.Select(c => c.Name).Concat(list.Select(c => c.Gift!.Caption)));
                var suggested = existing?.PosDiscountTypeId ?? matched;
                p.DiscountTypeId = suggested;

                if (existing == null)
                {
                    p.Changes.Add(new IikoImportChangeDto(TargetReward, target.Id, name, FieldName, null, name));
                    p.Changes.Add(new IikoImportChangeDto(TargetReward, target.Id, name, FieldPointsCost, null, costInt.ToString()));
                    if (codes.Count > 0)
                        p.Changes.Add(new IikoImportChangeDto(TargetReward, target.Id, name, FieldProducts, null, string.Join(",", codes)));
                    p.Changes.Add(new IikoImportChangeDto(TargetReward, target.Id, name, FieldSourceKey, null, sourceKey));
                    if (suggested != null)
                        p.Changes.Add(new IikoImportChangeDto(TargetReward, target.Id, name, FieldDiscountType, null, suggested));
                }
                else
                {
                    if (existing.PointsCost != costInt)
                        p.Changes.Add(new IikoImportChangeDto(TargetReward, target.Id, existing.Name, FieldPointsCost, existing.PointsCost.ToString(), costInt.ToString()));
                    var missing = codes.Where(c => !existing.ProductCodes.Contains(c, StringComparer.OrdinalIgnoreCase)).ToList();
                    if (missing.Count > 0)
                        p.Changes.Add(new IikoImportChangeDto(TargetReward, target.Id, existing.Name, FieldProducts, null, string.Join(",", missing)));
                    if (existing.PosDiscountTypeId == null && matched != null)
                        p.Changes.Add(new IikoImportChangeDto(TargetReward, target.Id, existing.Name, FieldDiscountType, null, matched));
                    if (p.Changes.Count > 0)
                        p.Changes.Insert(0, new IikoImportChangeDto(TargetReward, target.Id, existing.Name, FieldRewardId, null, existing.Id.ToString()));
                }

                var warnings = new List<string>();
                if (suggested == null)
                    warnings.Add("Kassa endirimini seçin — sifarişdə hədiyyə onunla tanınır" + (anySized ? " (ölçülü məhsullar yalnız endirimlə tanınır)" : "") + ".");
                if (target.TemplateType == CardTemplateType.ProgressBased && target.TotalSteps.HasValue && target.TotalSteps.Value != costInt)
                    warnings.Add($"Walvero kartı {target.TotalSteps} möhürdə hədiyyə göstərir, kassa {costInt} vahiddə verir — kart şablonunu uyğunlaşdırın.");
                if (warnings.Count > 0) p.Warning = string.Join(" ", warnings);

                Finish(p);
                // İdxal olunmuş, amma endirimi hələ seçilməmiş hədiyyə: endirim dialoqda seçilib tətbiq olunur
                if (existing != null && suggested == null)
                {
                    p.Applicable = true;
                    p.AlreadyApplied = false;
                    if (!p.Changes.Any(c => c.Field == FieldRewardId))
                        p.Changes.Insert(0,new IikoImportChangeDto(TargetReward, target.Id, existing.Name, FieldRewardId, null, existing.Id.ToString()));
                }
                p.SelectedByDefault = false;
                yield return p;
            }
        }

        private static bool AppliesTo(WalveroEarnRuleState r, WalveroProgramState target) => r.ProgramId == null || r.ProgramId == target.Id;

        private static bool Same(string? a, string? b)
            => !string.IsNullOrWhiteSpace(a) && !string.IsNullOrWhiteSpace(b) && string.Equals(a.Trim(), b.Trim(), StringComparison.OrdinalIgnoreCase);

        /// <summary>Sifarişlərdə görünən endirim — adı kampaniya adı və ya kassir mətni ilə eynidir (Cupcup: «Оплата со счета»).</summary>
        private static string? MatchDiscount(List<IikoDiscountOptionDto> observed, IEnumerable<string?> names)
        {
            foreach (var n in names.Where(n => !string.IsNullOrWhiteSpace(n)))
            {
                var hit = observed.FirstOrDefault(o => Same(o.Name, n));
                if (hit != null) return hit.Id;
            }
            return null;
        }

        private static void FillCashback(IikoImportProposalDto p, WalveroProgramState target, PosEarnSource source, string? walletId, string iikoProgramId, decimal percent)
        {
            AddProgramChange(p, target, "PosEarnSource", target.PosEarnSource.ToString(), source.ToString());
            AddProgramChange(p, target, "PosWalletId", target.PosWalletId, walletId);
            AddProgramChange(p, target, "PosExternalProgramId", target.PosExternalProgramId, iikoProgramId);
            if (target.TemplateType is CardTemplateType.PointsBased or CardTemplateType.TierBased)
            {
                var pct = (int)Math.Round(percent);
                if (target.BronzePercent != pct)
                    p.Changes.Add(new IikoImportChangeDto(TargetTemplate, target.Id, target.Name, "BronzePercent",
                        target.BronzePercent?.ToString(), pct.ToString()));
            }
        }

        private static void AddProgramChange(IikoImportProposalDto p, WalveroProgramState prog, string field, string? from, string? to)
        {
            if (string.Equals(from ?? "", to ?? "", StringComparison.OrdinalIgnoreCase)) return;
            p.Changes.Add(new IikoImportChangeDto(TargetProgram, prog.Id, prog.Name, field, from, to));
        }

        private static void Finish(IikoImportProposalDto p)
        {
            p.Applicable = p.Changes.Count > 0;
            p.AlreadyApplied = p.Changes.Count == 0;
            p.SelectedByDefault = p.Applicable && p.Warning == null;
        }

        // ───────────────────────── Sinxron vəziyyəti ─────────────────────────

        public static List<IikoSyncItemDto> Status(IikoCatalogDto catalog, WalveroPosState w)
        {
            var items = new List<IikoSyncItemDto>();
            var wallets = catalog.Wallets;

            // Kassa cüzdanları ↔ Walvero proqramları
            foreach (var wallet in wallets)
            {
                var linked = w.Programs.Where(p => string.Equals(p.PosWalletId, wallet.Id, StringComparison.OrdinalIgnoreCase)).ToList();
                items.Add(linked.Count > 0
                    ? new IikoSyncItemDto("ok", "wallet", $"Cüzdan «{wallet.Name}» → {string.Join(", ", linked.Select(l => l.Name))}", null)
                    : new IikoSyncItemDto(wallet.HasPayment || wallet.UsedBy.Count > 0 ? "warn" : "info", "wallet",
                        $"Cüzdan «{wallet.Name}» Walvero proqramına bağlanmayıb", "Bağlantılar səhifəsində proqrama cüzdan seçin və ya idxal edin."));
            }
            foreach (var p in w.Programs.Where(p => !string.IsNullOrWhiteSpace(p.PosWalletId)))
            {
                if (!wallets.Any(x => string.Equals(x.Id, p.PosWalletId, StringComparison.OrdinalIgnoreCase)) && catalog.Programs.Count > 0)
                    items.Add(new IikoSyncItemDto("error", "program", $"«{p.Name}» proqramının cüzdanı kassada tapılmadı", "Cüzdan silinib və ya dəyişib — yenidən seçin."));
            }

            // Kassa hədiyyələri ("N al → 1 pulsuz") ↔ Walvero hədiyyələri
            foreach (var ip in catalog.Programs.Where(p => p.IsActive))
            {
                foreach (var cost in ip.Campaigns.Where(c => c.IsActive && c.Gift is { CostPerGift: > 0 }).Select(c => c.Gift!.CostPerGift).Distinct())
                {
                    var key = $"{ip.Id}:{cost.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)}";
                    var reward = w.Rewards.FirstOrDefault(r => Same(r.PosSourceKey, key));
                    var label = $"Kassa hədiyyəsi ({(ip.Name ?? ip.Id).Trim()}, {cost:0.##} vahid)";
                    items.Add(reward == null
                        ? new IikoSyncItemDto("info", "reward", $"{label} Walvero-ya idxal olunmayıb", "Kassa verəndə Walvero kartından silinmir — lazımdırsa «Kassadan idxal et».")
                        : reward.PosDiscountTypeId == null
                            ? new IikoSyncItemDto("warn", "reward", $"{label} → «{reward.Name}», amma kassa endirimi seçilməyib", "Sifarişdə hədiyyə tanınmır — idxal dialoqunda endirimi seçin.")
                            : new IikoSyncItemDto("ok", "reward", $"{label} → «{reward.Name}»", null));
                }
            }

            // Riskli rejimlər
            var cashbackWallets = catalog.Programs.Where(ip => ip.IsActive)
                .SelectMany(ip => ip.Campaigns.Where(c => c.IsActive && c.Kind == IikoRuleParser.KindCashbackPercent)
                    .Select(c => c.WalletIds.FirstOrDefault() ?? ip.WalletId))
                .Where(x => x != null).ToHashSet(StringComparer.OrdinalIgnoreCase);
            // Bütün məhsullara aktiv keşbek kampaniyaları (cüzdan → kampaniya adı)
            var fullCashbackByWallet = catalog.Programs.Where(ip => ip.IsActive)
                .SelectMany(ip => ip.Campaigns
                    .Where(c => c.IsActive && c.Kind == IikoRuleParser.KindCashbackPercent && c.AllProducts && c.GuestCategoryIds.Count == 0)
                    .Select(c => (Wallet: c.WalletIds.FirstOrDefault() ?? ip.WalletId, c.Name)))
                .Where(x => x.Wallet != null)
                .GroupBy(x => x.Wallet!, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First().Name, StringComparer.OrdinalIgnoreCase);
            foreach (var p in w.Programs)
            {
                if (p.PosEarnSource == PosEarnSource.WalveroRules && p.PosWalletId != null
                    && fullCashbackByWallet.TryGetValue(p.PosWalletId, out var activeCampaign))
                    items.Add(new IikoSyncItemDto("warn", "program", $"«{p.Name}»: Walvero hesablayır, amma kassada «{activeCampaign}» keşbek kampaniyası da aktivdir",
                        "İkiqat yığım riski — kassada həmin kampaniyanı söndürün və ya «Kassa hesablayır» seçin."));
                if (p.PosEarnSource == PosEarnSource.Legacy && cashbackWallets.Count > 0 && p.Audience != ProgramAudience.Ambassador)
                    items.Add(new IikoSyncItemDto("info", "program", $"«{p.Name}»: kassa da, Walvero da hesablayır (köhnə rejim)",
                        "Walvero sifarişdən hesablayır; kassa cüzdan əməliyyatı gələndə kartın balansı kassanın balansı ilə yenilənir."));
                if (p.TemplateType == CardTemplateType.ProgressBased && p.PosEarnSource != PosEarnSource.WalveroRules)
                    items.Add(new IikoSyncItemDto("warn", "program", $"«{p.Name}»: möhür kartı, kassa cüzdanı güzgüsü açıqdır",
                        "Kassa balansı möhürləri əvəz edə bilər — «Walvero hesablayır» seçin."));
            }

            // Ambassador
            var amb = w.Programs.FirstOrDefault(p => p.Audience == ProgramAudience.Ambassador);
            if (amb != null)
            {
                if (string.IsNullOrWhiteSpace(w.AmbassadorCategoryId))
                {
                    var referenced = wallets.FirstOrDefault(x => string.Equals(x.Id, amb.PosWalletId, StringComparison.OrdinalIgnoreCase))?.GuestCategoryIds.FirstOrDefault();
                    items.Add(new IikoSyncItemDto(referenced != null ? "warn" : "info", "category", "Ambassador qonaq kateqoriyası təyin edilməyib",
                        referenced != null ? "Kassa kampaniyası kateqoriya şərti işlədir — kateqoriyanı seçin, yoxsa ambassador-lar kassada tanınmaz." : null));
                }
                else
                {
                    var gc = catalog.GuestCategories.FirstOrDefault(g => string.Equals(g.Id, w.AmbassadorCategoryId, StringComparison.OrdinalIgnoreCase));
                    items.Add(gc != null && gc.Name != null
                        ? new IikoSyncItemDto("ok", "category", $"Ambassador qonaq kateqoriyası: «{gc.Name}»", null)
                        : new IikoSyncItemDto("error", "category", "Ambassador qonaq kateqoriyası kassada tapılmadı", "Kateqoriyanı yenidən seçin."));
                }
                if (!w.AmbassadorsEnabled)
                    items.Add(new IikoSyncItemDto("warn", "category", "Ambassador proqramı var, amma tenant-da ambassador funksiyası söndürülüb", "Super admin açmalıdır."));
            }

            // Kataloq / nomenklatura / qaydalar
            foreach (var v in catalog.Venues.Where(v => v.Error != null))
                items.Add(new IikoSyncItemDto("error", "venue", $"{v.BranchName ?? v.DisplayName ?? v.OrganizationId}: kassa cavab vermədi", v.Error));
            var nom = catalog.Nomenclature;
            items.Add(nom?.FetchedAtUtc != null
                ? new IikoSyncItemDto("ok", "nomenclature", $"Məhsul kataloqu: {nom.ProductCount} məhsul, {nom.Categories.Count} kateqoriya (yenilənib {nom.FetchedAtUtc:dd.MM.yyyy HH:mm} UTC)", null)
                : new IikoSyncItemDto(nom?.ObservedProductCount > 0 ? "info" : "warn", "nomenclature",
                    $"Məhsul kataloqu kassadan yüklənməyib{(nom?.ObservedProductCount > 0 ? $" — sifarişlərdən {nom.ObservedProductCount} məhsul" : "")}",
                    "Bağlantılar → məhsul seçimində «yenilə» düyməsi ilə yükləyin."));
            items.Add(new IikoSyncItemDto("info", "rules", $"Walvero məhsul qaydaları: {w.EarnRuleCount}, bonussuz məhsul istisnaları: {w.ExclusionCount}", null));

            return items;
        }
    }
}

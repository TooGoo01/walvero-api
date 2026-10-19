using Loyalty.Business.Services.Abstractions.Main;
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

        public const string TargetProgram = "program";
        public const string TargetNewAmbassadorProgram = "new_ambassador_program";
        public const string TargetTemplate = "template";
        public const string TargetIntegration = "integration";

        public const string UnitCurrency = "məhsul";

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
                foreach (var c in ip.Campaigns.Where(c => c.IsActive))
                {
                    var walletId = c.Payment?.WalletId ?? c.WalletIds.FirstOrDefault() ?? ip.WalletId;
                    var label = $"{ip.Name} → {c.Name}";

                    // 1) Ümumi keşbek — adi proqram kassa cüzdanını güzgüləyir
                    if (c.Kind == IikoRuleParser.KindCashbackPercent && c.AllProducts && c.GuestCategoryIds.Count == 0 && c.Percent.HasValue)
                    {
                        var p = new IikoImportProposalDto
                        {
                            Key = $"cashback:{c.Id}",
                            Kind = KindCashback,
                            Title = $"Keşbek {c.Percent:0.##}% — {label}",
                            Explanation = "Keşbeki kassa hesablayır; Walvero kartı kassa cüzdanının balansını göstərir (ikiqat hesab olmasın).",
                        };
                        if (publicTarget == null)
                        {
                            p.Applicable = false;
                            p.Warning = "Adi (Public) Walvero proqramı yoxdur.";
                        }
                        else if (!claimedPrograms.Add(publicTarget.Id))
                        {
                            p.Applicable = false;
                            p.Warning = $"«{publicTarget.Name}» artıq başqa keşbek kampaniyasına bağlanır — yalnız biri seçilə bilər.";
                        }
                        else
                        {
                            AddProgramChange(p, publicTarget, "PosEarnSource", publicTarget.PosEarnSource.ToString(), nameof(PosEarnSource.PosMirror));
                            AddProgramChange(p, publicTarget, "PosWalletId", publicTarget.PosWalletId, walletId);
                            AddProgramChange(p, publicTarget, "PosExternalProgramId", publicTarget.PosExternalProgramId, ip.Id);
                            if (publicTarget.TemplateType is CardTemplateType.PointsBased or CardTemplateType.TierBased)
                            {
                                var pct = (int)Math.Round(c.Percent.Value);
                                if (publicTarget.BronzePercent != pct)
                                    p.Changes.Add(new IikoImportChangeDto(TargetTemplate, publicTarget.Id, publicTarget.Name, "BronzePercent",
                                        publicTarget.BronzePercent?.ToString(), pct.ToString()));
                            }
                            Finish(p);
                        }
                        proposals.Add(p);
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

            // Riskli rejimlər
            var cashbackWallets = catalog.Programs.Where(ip => ip.IsActive)
                .SelectMany(ip => ip.Campaigns.Where(c => c.IsActive && c.Kind == IikoRuleParser.KindCashbackPercent)
                    .Select(c => c.WalletIds.FirstOrDefault() ?? ip.WalletId))
                .Where(x => x != null).ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (var p in w.Programs)
            {
                if (p.PosEarnSource == PosEarnSource.Legacy && cashbackWallets.Count > 0 && p.Audience != ProgramAudience.Ambassador)
                    items.Add(new IikoSyncItemDto("warn", "program", $"«{p.Name}»: qazanc mənbəyi «hər ikisi (köhnə)», kassada keşbek kampaniyası var",
                        "İkiqat hesab / balans fərqi riski — «Kassa hesablayır» və ya «Walvero hesablayır» seçin."));
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

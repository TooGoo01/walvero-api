using Loyalty.Business.Services.Abstractions.Main;

namespace Loyalty.Business.Helpers
{
    /// <summary>
    /// Org-lar üzrə iiko proqramlarını / cüzdanları / qonaq kateqoriyalarını təkrarsız kataloqa birləşdirir (DB-siz, test olunur).
    /// iiko müştəri/cüzdanları org-lar arası paylaşıldığı üçün eyni proqram bir neçə venue-da görünə bilər.
    /// </summary>
    public static class IikoCatalogMerge
    {
        public static List<IikoCatalogProgramDto> Programs(IEnumerable<(string VenueName, List<IikoProgramSummary> Programs)> perVenue)
        {
            var byId = new Dictionary<string, IikoCatalogProgramDto>(StringComparer.OrdinalIgnoreCase);
            foreach (var (venue, programs) in perVenue)
            {
                foreach (var p in programs)
                {
                    if (!byId.TryGetValue(p.Id, out var dto))
                    {
                        dto = new IikoCatalogProgramDto
                        {
                            Id = p.Id,
                            Name = p.Name,
                            IsActive = p.IsActive,
                            WalletId = p.WalletId,
                            CashbackPercent = IikoRuleParser.ProgramCashbackPercent(p),
                            Campaigns = p.Campaigns
                        };
                        byId[p.Id] = dto;
                    }
                    if (!string.IsNullOrWhiteSpace(venue) && !dto.Venues.Contains(venue))
                        dto.Venues.Add(venue);
                }
            }
            return byId.Values.OrderByDescending(p => p.IsActive).ThenBy(p => p.Name).ToList();
        }

        /// <summary>Cüzdanlar: proqramın WalletId-si + kampaniya action-larındakı WalletId-lər.</summary>
        public static List<IikoCatalogWalletDto> Wallets(IEnumerable<IikoCatalogProgramDto> programs)
        {
            var byId = new Dictionary<string, IikoCatalogWalletDto>(StringComparer.OrdinalIgnoreCase);
            IikoCatalogWalletDto Get(string id, IikoCatalogProgramDto p)
            {
                if (!byId.TryGetValue(id, out var w))
                    byId[id] = w = new IikoCatalogWalletDto { Id = id, Name = p.Name, ProgramId = p.Id, ProgramName = p.Name };
                return w;
            }

            foreach (var p in programs)
            {
                if (!string.IsNullOrWhiteSpace(p.WalletId)) Get(p.WalletId!, p);
                foreach (var c in p.Campaigns)
                {
                    var label = $"{c.Name} ({c.KindLabel})";
                    foreach (var wid in c.WalletIds)
                    {
                        var w = Get(wid, p);
                        if (!w.UsedBy.Contains(label)) w.UsedBy.Add(label);
                        foreach (var g in c.GuestCategoryIds)
                            if (!w.GuestCategoryIds.Contains(g, StringComparer.OrdinalIgnoreCase)) w.GuestCategoryIds.Add(g);
                    }
                    if (c.Payment?.WalletId is { Length: > 0 } pw)
                    {
                        var w = Get(pw, p);
                        w.HasPayment = true;
                        w.UnitsPerItem ??= c.Payment.UnitsPerItem;
                    }
                }
            }
            return byId.Values.ToList();
        }

        /// <summary>Qonaq kateqoriyaları: iiko siyahısı (org-lar üzrə təkrarsız) + kampaniya şərtlərində istinad olunanlar.</summary>
        public static List<IikoCatalogGuestCategoryDto> GuestCategories(
            IEnumerable<IikoGuestCategory> fetched, IEnumerable<IikoCatalogProgramDto> programs)
        {
            var byId = new Dictionary<string, IikoCatalogGuestCategoryDto>(StringComparer.OrdinalIgnoreCase);
            foreach (var g in fetched)
            {
                var id = g.Id.ToString();
                if (!byId.ContainsKey(id))
                    byId[id] = new IikoCatalogGuestCategoryDto { Id = id, Name = g.Name, IsActive = g.IsActive, IsDefaultForNewGuests = g.IsDefaultForNewGuests };
            }
            foreach (var p in programs)
            foreach (var c in p.Campaigns)
            foreach (var id in c.GuestCategoryIds.Concat(c.ExcludedGuestCategoryIds))
            {
                if (!byId.TryGetValue(id, out var dto))
                    byId[id] = dto = new IikoCatalogGuestCategoryDto { Id = id, IsActive = true }; // iiko siyahısında yoxdur (ad bilinmir)
                var label = c.Name ?? c.Id;
                if (!dto.UsedBy.Contains(label)) dto.UsedBy.Add(label);
            }
            return byId.Values.OrderBy(g => g.Name ?? "~").ToList();
        }
    }
}

using Loyalty.Business.Services.Abstractions.Main;

namespace Loyalty.Business.Helpers
{
    /// <summary>
    /// Kassa endirim səviyyələri: aktiv iiko kampaniyası + sifarişə faiz endirimi + qonaq kateqoriyası şərti
    /// (Carpax "ENDİRİM 20%" / "Endirim 30%"). Tenant admin müştərini səviyyəyə bağlayanda Walvero onu həmin
    /// kateqoriyaya yazır; endirimi iiko kampaniyası özü tətbiq edir. Pure, test olunur.
    /// </summary>
    public static class IikoDiscountTiers
    {
        public static List<IikoDiscountTierDto> Build(
            IEnumerable<IikoCatalogProgramDto> programs,
            IEnumerable<IikoCatalogGuestCategoryDto> guestCategories)
        {
            var names = guestCategories
                .Where(g => !string.IsNullOrWhiteSpace(g.Id))
                .GroupBy(g => g.Id, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First().Name, StringComparer.OrdinalIgnoreCase);

            var tiers = new List<IikoDiscountTierDto>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var program in programs)
            {
                foreach (var c in program.Campaigns)
                {
                    if (!c.IsActive || c.Kind != IikoRuleParser.KindDiscountPercent) continue;
                    if (c.Percent is not decimal percent || percent <= 0) continue;
                    foreach (var categoryId in c.GuestCategoryIds)
                    {
                        if (string.IsNullOrWhiteSpace(categoryId) || !seen.Add(categoryId)) continue;
                        tiers.Add(new IikoDiscountTierDto
                        {
                            CampaignId = c.Id,
                            CampaignName = c.Name ?? $"Endirim {percent:0.##}%",
                            Percent = percent,
                            GuestCategoryId = categoryId,
                            GuestCategoryName = names.TryGetValue(categoryId, out var n) ? n : null,
                            RequiresManualActivation = c.RequiresManualActivation
                        });
                    }
                }
            }
            return tiers.OrderByDescending(t => t.Percent).ThenBy(t => t.CampaignName, StringComparer.OrdinalIgnoreCase).ToList();
        }
    }
}

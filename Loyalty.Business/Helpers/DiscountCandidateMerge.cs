namespace Loyalty.Business.Helpers
{
    public sealed record DiscountCandidateSource(string Id, string? Name, decimal? Percent, bool IsDeleted, bool IsManual);
    public sealed record ObservedDiscountSource(string Id, string? Name, DateTime LastSeenAtUtc);

    public sealed class DiscountCandidate
    {
        public string Id { get; init; } = string.Empty;
        public string? Name { get; init; }
        public decimal? Percent { get; init; }
        public bool IsManual { get; init; }
        /// <summary>iiko endirim siyahısında var.</summary>
        public bool InCatalog { get; init; }
        /// <summary>Kassadan keçən sifarişdə görünüb (son dəfə).</summary>
        public DateTime? LastSeenAtUtc { get; init; }
        /// <summary>Artıq hədiyyə endirimi kimi bağlanıb.</summary>
        public bool Linked { get; init; }
        /// <summary>Adında "Walvero" var və hələ bağlanmayıb — seçimdə əvvəlcədən işarələnir.</summary>
        public bool Suggested { get; init; }
    }

    /// <summary>
    /// Hədiyyə (loyalty) endirimi seçimi: iiko endirim siyahısı + sifarişlərdə görünən endirimlər birləşir (ID üzrə).
    /// Silinmiş iiko endirimi yalnız sifarişdə görünübsə qalır. Sıra: təklif → bağlı olmayan → son görünmə → ad.
    /// </summary>
    public static class DiscountCandidateMerge
    {
        public const string SuggestKeyword = "walvero";

        public static List<DiscountCandidate> Merge(
            IEnumerable<DiscountCandidateSource> catalog,
            IEnumerable<ObservedDiscountSource> observed,
            IEnumerable<string> linkedIds)
        {
            var cmp = StringComparer.OrdinalIgnoreCase;
            var linked = new HashSet<string>(linkedIds.Select(x => x.Trim()), cmp);
            var byId = new Dictionary<string, DiscountCandidateSource>(cmp);
            foreach (var c in catalog)
                if (!string.IsNullOrWhiteSpace(c.Id)) byId.TryAdd(c.Id.Trim(), c);
            var seen = new Dictionary<string, ObservedDiscountSource>(cmp);
            foreach (var o in observed)
            {
                if (string.IsNullOrWhiteSpace(o.Id)) continue;
                var id = o.Id.Trim();
                if (!seen.TryGetValue(id, out var prev) || o.LastSeenAtUtc > prev.LastSeenAtUtc) seen[id] = o;
            }

            var result = new List<DiscountCandidate>();
            foreach (var id in byId.Keys.Concat(seen.Keys).Distinct(cmp))
            {
                byId.TryGetValue(id, out var cat);
                seen.TryGetValue(id, out var obs);
                if (cat is { IsDeleted: true } && obs == null) continue;
                var name = !string.IsNullOrWhiteSpace(cat?.Name) ? cat!.Name!.Trim() : obs?.Name?.Trim();
                var isLinked = linked.Contains(id);
                result.Add(new DiscountCandidate
                {
                    Id = id,
                    Name = name,
                    Percent = cat?.Percent,
                    IsManual = cat?.IsManual ?? false,
                    InCatalog = cat != null && !cat.IsDeleted,
                    LastSeenAtUtc = obs?.LastSeenAtUtc,
                    Linked = isLinked,
                    Suggested = !isLinked && name != null && name.Contains(SuggestKeyword, StringComparison.OrdinalIgnoreCase)
                });
            }

            return result
                .OrderByDescending(x => x.Suggested)
                .ThenBy(x => x.Linked)
                .ThenByDescending(x => x.LastSeenAtUtc ?? DateTime.MinValue)
                .ThenBy(x => x.Name ?? x.Id, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
        }
    }
}

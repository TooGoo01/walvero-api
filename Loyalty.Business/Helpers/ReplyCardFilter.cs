using Loyalty.Business.Dtos.Chatbot;

namespace Loyalty.Business.Helpers
{
    /// <summary>
    /// AI cavabının altındakı məkan kartları cavab mətnini izləsin: tool-ların topladığı bütün məkanlardan
    /// yalnız adı cavabda keçənlər, cavabdakı sıra ilə. Heç biri tapılmasa — əvvəlki davranış (hamısı).
    /// Pure, test olunur.
    /// </summary>
    public static class ReplyCardFilter
    {
        private static readonly HashSet<string> Generic = new(StringComparer.Ordinal)
        {
            "restaurant", "restoran", "restorani", "cafe", "kafe", "baku", "baki", "by", "the", "and",
            "bar", "lounge", "house", "hotel", "coffee", "pub"
        };

        public static List<PlaceResultDto> Filter(IReadOnlyList<PlaceResultDto> places, string? reply)
        {
            if (places.Count == 0 || string.IsNullOrWhiteSpace(reply)) return places.ToList();

            var replyNorm = ReferencePlaceRanking.Normalize(reply);
            var replyCompact = ReferencePlaceRanking.Compact(reply);
            var replyWords = Words(replyNorm);

            var hits = new List<(PlaceResultDto Place, int Pos)>();
            foreach (var p in places)
            {
                var pos = Position(p.Name, replyNorm, replyCompact, replyWords);
                if (pos >= 0) hits.Add((p, pos));
            }

            return hits.Count == 0
                ? places.ToList()
                : hits.OrderBy(h => h.Pos).Select(h => h.Place).ToList();
        }

        /// <summary>Ad cavabda haradadır (-1 = yoxdur). Tam ad (boşluqsuz) və ya fərqləndirici sözlərin hamısı.</summary>
        private static int Position(string name, string replyNorm, string replyCompact, HashSet<string> replyWords)
        {
            var compact = ReferencePlaceRanking.Compact(name);
            if (compact.Length >= 3)
            {
                var i = replyCompact.IndexOf(compact, StringComparison.Ordinal);
                if (i >= 0) return i;
            }

            var distinctive = Words(ReferencePlaceRanking.Normalize(name))
                .Where(w => w.Length >= 3 && !Generic.Contains(w))
                .ToList();
            if (distinctive.Count == 0 || !distinctive.All(replyWords.Contains)) return -1;

            // mövqe: ilk fərqləndirici sözün cavabdakı yeri
            var first = replyNorm.IndexOf(distinctive[0], StringComparison.Ordinal);
            return first < 0 ? int.MaxValue / 2 : first;
        }

        private static HashSet<string> Words(string norm)
        {
            var set = new HashSet<string>(StringComparer.Ordinal);
            var cur = new System.Text.StringBuilder();
            foreach (var ch in norm)
            {
                if (char.IsLetterOrDigit(ch)) cur.Append(ch);
                else if (cur.Length > 0) { set.Add(cur.ToString()); cur.Clear(); }
            }
            if (cur.Length > 0) set.Add(cur.ToString());
            return set;
        }
    }
}

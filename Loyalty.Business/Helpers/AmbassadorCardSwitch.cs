using Loyalty.DataAccess.Enums;

namespace Loyalty.Business.Helpers
{
    /// <summary>Ambassador kart keçidinin təmiz qaydaları (DB-siz, test olunur).</summary>
    public static class AmbassadorCardSwitch
    {
        public const int MaxCardNumberLength = 50;

        /// <summary>Pul balansı yalnız xal/cashback kartları arasında köçürülür; möhür (ProgressBased) kartı ilə yox.</summary>
        public static bool CanCarryOver(CardTemplateType from, CardTemplateType to)
            => from != CardTemplateType.ProgressBased && to != CardTemplateType.ProgressBased;

        /// <summary>
        /// Yeni kartın CardNumber namizədi: "{base}-A", sonra "{base}-A2", "{base}-A3"…; 50 simvola sığdırılır
        /// (suffiks həmişə saxlanır, baza kəsilir).
        /// </summary>
        public static string CardNumberCandidate(string baseNumber, string suffix, int attempt)
        {
            var tail = "-" + suffix + (attempt > 0 ? (attempt + 1).ToString() : "");
            var b = baseNumber.Trim();
            if (b.Length + tail.Length > MaxCardNumberLength)
                b = b[..(MaxCardNumberLength - tail.Length)];
            return b + tail;
        }
    }
}

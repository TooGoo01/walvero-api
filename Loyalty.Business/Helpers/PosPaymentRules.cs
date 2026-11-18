using Loyalty.Business.Services.Abstractions.Main;

namespace Loyalty.Business.Helpers
{
    /// <summary>
    /// Kassa ödəniş növləri üzrə qaydalar. Compliment (pulsuz, müəssisə hesabına) ilə ödənən hissə
    /// cashback/xal bazasına düşmür.
    /// </summary>
    public static class PosPaymentRules
    {
        private static readonly string[] ComplimentMarkers =
        {
            "compliment", "complement", "kompliment", "komplement", "комплимент"
        };

        public static bool IsCompliment(string? paymentTypeName)
        {
            if (string.IsNullOrWhiteSpace(paymentTypeName)) return false;
            var name = paymentTypeName.Trim();
            foreach (var marker in ComplimentMarkers)
                if (name.Contains(marker, StringComparison.OrdinalIgnoreCase))
                    return true;
            return false;
        }

        /// <summary>Compliment ödənişlərinin cəmi. Walvero cüzdan ödənişi (Credential dolu) sayılmır.</summary>
        public static decimal ComplimentSum(IEnumerable<IikoOrderPayment>? payments)
            => (payments ?? Enumerable.Empty<IikoOrderPayment>())
                .Where(p => string.IsNullOrWhiteSpace(p.Credential) && IsCompliment(p.PaymentTypeName))
                .Sum(p => Math.Max(0m, p.Sum));
    }
}

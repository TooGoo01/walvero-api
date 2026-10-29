using System.Globalization;
using System.Text.RegularExpressions;

namespace Loyalty.Business.Helpers
{
    /// <summary>Müştərinin kassa endirimi faizi (Wallet kartında «Endirim N%»). Pure — test olunur.</summary>
    public static class PosDiscount
    {
        private static readonly Regex PercentInName = new(@"(\d+(?:[.,]\d+)?)\s*%", RegexOptions.Compiled);

        /// <summary>
        /// Saxlanmış faiz; yoxdursa (sütundan əvvəl təyin olunmuş müştərilər) kampaniya adından ("ENDİRİM 20%") çıxarılır.
        /// Kateqoriya yoxdursa null.
        /// </summary>
        public static decimal? Percent(string? guestCategoryId, string? guestCategoryName, decimal? storedPercent)
        {
            if (string.IsNullOrWhiteSpace(guestCategoryId)) return null;
            if (storedPercent is > 0) return storedPercent;
            if (string.IsNullOrWhiteSpace(guestCategoryName)) return null;
            var m = PercentInName.Match(guestCategoryName);
            return m.Success && decimal.TryParse(m.Groups[1].Value.Replace(',', '.'), NumberStyles.Number, CultureInfo.InvariantCulture, out var p) && p > 0
                ? p : null;
        }
    }
}

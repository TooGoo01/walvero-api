namespace Loyalty.Business.Utilities
{
    /// <summary>
    /// iiko-ya göndərilən kart identifikatorunu qısaldır — tam barcode əvəzinə yalnız son N rəqəm.
    /// iiko customer cardTrack/cardNumber üçün istifadə olunur; daxili LoyaltyCard tam saxlanılır.
    /// </summary>
    public static class IikoCard
    {
        public const int DigitCount = 6;

        /// <summary>Barcode-un son <see cref="DigitCount"/> simvolu (qısadırsa olduğu kimi). null/boş → "".</summary>
        public static string Short(string? full)
        {
            if (string.IsNullOrWhiteSpace(full))
                return string.Empty;
            var v = full.Trim();
            return v.Length > DigitCount ? v.Substring(v.Length - DigitCount) : v;
        }
    }
}

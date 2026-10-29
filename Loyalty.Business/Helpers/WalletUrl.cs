namespace Loyalty.Business.Helpers
{
    /// <summary>
    /// Google Wallet şəkil URL-ləri: fayl adında boşluq / qeyri-ASCII ("Adsız tasarım (59).png") olanda API 400 "Invalid URL" qaytarır.
    /// Path percent-encode olunur (blob/CDN eyni faylı qaytarır); artıq encode olunmuş URL dəyişmir. Pure — test olunur.
    /// </summary>
    public static class WalletUrl
    {
        public static string? Safe(string? url)
        {
            if (string.IsNullOrWhiteSpace(url)) return url;
            return System.Uri.TryCreate(url.Trim(), System.UriKind.Absolute, out var uri) ? uri.AbsoluteUri : url;
        }
    }
}

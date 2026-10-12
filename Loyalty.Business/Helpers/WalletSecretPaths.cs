namespace Loyalty.Business.Helpers
{
    /// <summary>
    /// Wallet imza sirlərinin (Apple .p12, Google service account JSON) ünvanları üçün pure köməkçilər.
    /// Öz storage-imizdəki (blob və ya CDN) linklər authenticated oxunur — anonim HTTP ilə yox.
    /// </summary>
    public static class WalletSecretPaths
    {
        /// <summary>Öz Azure Blob hostlarımız: birbaşa storage və onun önündəki Front Door CDN (eyni path: /container/blob).</summary>
        public static readonly string[] OwnBlobHosts =
        {
            "walverostorage.blob.core.windows.net",
            "cdn.walvero.com"
        };

        /// <summary>
        /// Öz storage linkini (container, blob) hissələrinə ayırır. Query (SAS və s.) atılır, blob adı URL-decode olunur.
        /// Başqa host, nisbi yol və ya natamam path → false.
        /// </summary>
        public static bool TryParseOwnBlobUrl(string? url, out string container, out string blobName)
        {
            container = string.Empty;
            blobName = string.Empty;
            if (string.IsNullOrWhiteSpace(url)) return false;
            if (!Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri)) return false;
            if (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp) return false;
            if (!OwnBlobHosts.Any(h => string.Equals(h, uri.Host, StringComparison.OrdinalIgnoreCase))) return false;

            var path = uri.AbsolutePath.Trim('/');
            var slash = path.IndexOf('/');
            if (slash <= 0 || slash == path.Length - 1) return false;

            container = Uri.UnescapeDataString(path[..slash]);
            blobName = Uri.UnescapeDataString(path[(slash + 1)..]);
            return container.Length > 0 && blobName.Length > 0;
        }

        /// <summary>Şifrələnmiş parol markeri (köhnə açıq mətn parollarla qarışmasın deyə).</summary>
        public const string EncryptedPrefix = "enc:v1:";

        public static bool IsEncrypted(string? stored)
            => stored != null && stored.StartsWith(EncryptedPrefix, StringComparison.Ordinal);

        /// <summary>Saxlanılacaq dəyər: prefiks + şifrə mətni. Boş → boş.</summary>
        public static string Protect(string? plain, Func<string, string> encrypt)
            => string.IsNullOrEmpty(plain) ? string.Empty : EncryptedPrefix + encrypt(plain);

        /// <summary>
        /// Saxlanılan parolu açır: prefiksli → decrypt; prefikssiz (köhnə açıq mətn) → olduğu kimi.
        /// Decrypt alınmasa null (çağıran xəta verir).
        /// </summary>
        public static string? Reveal(string? stored, Func<string, string?> decrypt)
        {
            if (stored == null) return null;
            if (!IsEncrypted(stored)) return stored;
            return decrypt(stored[EncryptedPrefix.Length..]);
        }
    }
}

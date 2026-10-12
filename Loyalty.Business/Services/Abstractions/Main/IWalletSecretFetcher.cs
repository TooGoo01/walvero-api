namespace Loyalty.Business.Services.Abstractions.Main
{
    /// <summary>
    /// Wallet imza sirlərini (Apple .p12 / WWDR .cer, Google service account JSON) oxuyur.
    /// Öz blob/CDN linklərimiz storage açarı ilə (authenticated) endirilir — konteyner private ola bilər.
    /// Fayl lazım olanda public olmayan temp qovluğa yazılır (wwwroot-a YOX).
    /// </summary>
    public interface IWalletSecretFetcher
    {
        /// <summary>URL (öz blob/CDN və ya digər http) və ya lokal fayl yolundan bytes.</summary>
        Task<byte[]> GetBytesAsync(string pathOrUrl, CancellationToken ct = default);

        /// <summary>Sinxron variant (ApplePassSigner.Sign sinxrondur).</summary>
        byte[] GetBytes(string pathOrUrl);

        /// <summary>Sirri public olmayan lokal fayla yazır və absolute yolu qaytarır (təkrar çağırışda keşdən).</summary>
        Task<string> MaterializeAsync(string pathOrUrl, string defaultExtension, CancellationToken ct = default);

        /// <summary>DB-də saxlanan .p12 parolunu açır (enc:v1: prefiksli → decrypt, köhnə açıq mətn → olduğu kimi).</summary>
        string RevealPassword(string? stored);
    }
}

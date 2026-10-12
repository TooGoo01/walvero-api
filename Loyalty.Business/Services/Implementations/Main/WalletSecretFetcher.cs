using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using Azure.Storage.Blobs;
using Loyalty.Business.Helpers;
using Loyalty.Business.Services.Abstractions.Main;
using Loyalty.Infrastructure.Utilities.Security.Encryption;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Loyalty.Business.Services.Implementations.Main
{
    /// <summary>
    /// Singleton. Öz storage linkləri → BlobServiceClient (connection string "Storage:Azure") ilə oxunur,
    /// beləliklə wallet konteynerləri anonim girişə bağlana bilər. Yaddaş keşi 30 dəq, lokal fayl keşi 12 saat.
    /// Heç vaxt sirrin məzmununu və ya tam URL-ni loglamır.
    /// </summary>
    public sealed class WalletSecretFetcher : IWalletSecretFetcher
    {
        private static readonly TimeSpan MemoryTtl = TimeSpan.FromMinutes(30);
        private static readonly TimeSpan FileTtl = TimeSpan.FromHours(12);

        private readonly IConfiguration _configuration;
        private readonly IHttpClientFactory _http;
        private readonly ILogger<WalletSecretFetcher> _logger;
        private readonly Lazy<BlobServiceClient?> _blobService;
        private readonly Lazy<SensitiveDataProtector> _protector;
        private readonly ConcurrentDictionary<string, (byte[] Bytes, DateTime FetchedAtUtc)> _memory = new();
        private readonly string _cacheDir;

        public WalletSecretFetcher(IConfiguration configuration, IHttpClientFactory http, ILogger<WalletSecretFetcher> logger)
        {
            _configuration = configuration;
            _http = http;
            _logger = logger;
            _blobService = new Lazy<BlobServiceClient?>(() =>
            {
                var cs = _configuration["Storage:Azure"];
                return string.IsNullOrWhiteSpace(cs) ? null : new BlobServiceClient(cs);
            });
            _protector = new Lazy<SensitiveDataProtector>(() => new SensitiveDataProtector(_configuration));
            _cacheDir = Path.Combine(Path.GetTempPath(), "walvero-wallet-secrets");
        }

        public byte[] GetBytes(string pathOrUrl)
            => GetBytesAsync(pathOrUrl, CancellationToken.None).GetAwaiter().GetResult();

        public async Task<byte[]> GetBytesAsync(string pathOrUrl, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(pathOrUrl))
                throw new InvalidOperationException("Wallet secret path is empty.");
            var key = pathOrUrl.Trim();

            // Lokal fayl (bundled WWDR, materialize olunmuş fayl və s.)
            if (!IsHttp(key))
                return await File.ReadAllBytesAsync(Path.GetFullPath(key.Trim('"')), ct);

            if (_memory.TryGetValue(key, out var hit) && DateTime.UtcNow - hit.FetchedAtUtc < MemoryTtl)
                return hit.Bytes;

            byte[] bytes;
            if (WalletSecretPaths.TryParseOwnBlobUrl(key, out var container, out var blobName))
            {
                var svc = _blobService.Value
                          ?? throw new InvalidOperationException("Storage:Azure connection string is not configured.");
                try
                {
                    var resp = await svc.GetBlobContainerClient(container).GetBlobClient(blobName).DownloadContentAsync(ct);
                    bytes = resp.Value.Content.ToArray();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Wallet secret blob download failed. Container={Container}, Blob={Blob}", container, SafeName(blobName));
                    throw;
                }
            }
            else
            {
                // Legacy (nisbi /resource/... → appapi) — öz storage-imiz deyil
                var client = _http.CreateClient("wallet");
                using var res = await client.GetAsync(key, ct);
                res.EnsureSuccessStatusCode();
                bytes = await res.Content.ReadAsByteArrayAsync(ct);
            }

            _memory[key] = (bytes, DateTime.UtcNow);
            return bytes;
        }

        public async Task<string> MaterializeAsync(string pathOrUrl, string defaultExtension, CancellationToken ct = default)
        {
            var key = pathOrUrl.Trim();
            if (!IsHttp(key))
                return Path.GetFullPath(key.Trim('"'));

            var ext = ExtensionOf(key, defaultExtension);
            var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key)))[..24].ToLowerInvariant();
            var target = Path.Combine(_cacheDir, hash + ext);

            if (File.Exists(target) && DateTime.UtcNow - File.GetLastWriteTimeUtc(target) < FileTtl)
                return target;

            var bytes = await GetBytesAsync(key, ct);
            Directory.CreateDirectory(_cacheDir);
            var tmp = target + "." + Guid.NewGuid().ToString("N") + ".tmp";
            await File.WriteAllBytesAsync(tmp, bytes, ct);
            File.Move(tmp, target, overwrite: true);
            return target;
        }

        public string RevealPassword(string? stored)
        {
            var plain = WalletSecretPaths.Reveal(stored, cipher => _protector.Value.Decrypt(cipher));
            if (plain == null && stored != null)
                throw new InvalidOperationException("Apple certificate password could not be decrypted (Encryption:Key?).");
            return plain ?? string.Empty;
        }

        private static bool IsHttp(string s)
            => s.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || s.StartsWith("https://", StringComparison.OrdinalIgnoreCase);

        private static string ExtensionOf(string url, string fallback)
        {
            try
            {
                var ext = Path.GetExtension(new Uri(url).AbsolutePath);
                return string.IsNullOrWhiteSpace(ext) || ext.Length > 8 ? fallback : ext.ToLowerInvariant();
            }
            catch { return fallback; }
        }

        private static string SafeName(string blobName)
        {
            // Yalnız uzantı + uzunluq — adın özü (təxmin olunan hissə) loga düşməsin
            return $"*{Path.GetExtension(blobName)} (len {blobName.Length})";
        }
    }
}

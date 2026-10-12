using Loyalty.Business.Dtos.Post.Main;
using Loyalty.Business.Services.Abstractions.Main;
using Loyalty.DataAccess.Entities.Main;
using Loyalty.DataAccess.Enums;
using Loyalty.DataAccess.UnitOfWorks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using File = System.IO.File;

namespace Loyalty.Business.Services.Implementations.Main
{
    public sealed class WalletCredentialProvider : IWalletCredentialProvider
    {
        private readonly IUnitOfWork _uow;
        private readonly IAppEnvironment _env;
        private readonly IWalletSecretFetcher _secrets;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ILogger<WalletCredentialProvider> _logger;

        // Walvero public host – istəsən sonra config-dən oxuya bilərsən
        private static readonly Uri WalveroBaseUri = new("https://appapi.walvero.com/");

        public WalletCredentialProvider(
            IUnitOfWork uow,
            IAppEnvironment env,
            IWalletSecretFetcher secrets,
            IHttpContextAccessor httpContextAccessor,
            ILogger<WalletCredentialProvider> logger)
        {
            _uow = uow;
            _env = env;
            _secrets = secrets;
            _httpContextAccessor = httpContextAccessor;
            _logger = logger;
        }

        public async Task<AppleWalletRuntimeCreds> GetAppleAsync(int tenantId, CancellationToken ct = default)
        {
            using var scope = _logger.BeginScope(new Dictionary<string, object?>
            {
                ["TenantId"] = tenantId,
                ["Platform"] = "AppleWallet"
            });

            _logger.LogInformation("Fetching Apple Wallet credentials...");

            var repo = _uow.Repository<WalletCredential>();
            var cred = repo.GetAll()
                           .FirstOrDefault(x => x.TenantId == tenantId &&
                                                x.Platform == WalletPlatform.AppleWallet &&
                                                x.IsActive)
                       ?? throw new InvalidOperationException("Apple Wallet credential tapılmadı.");

            // minimal validasiya
            if (string.IsNullOrWhiteSpace(cred.TeamIdentifier) ||
                string.IsNullOrWhiteSpace(cred.PassTypeIdentifier) ||
                string.IsNullOrWhiteSpace(cred.AppleCertificateUrl) ||
                string.IsNullOrWhiteSpace(cred.CertificatePassword))
            {
                _logger.LogWarning("Apple credential incomplete (Team/PassType/P12/Password required).");
                throw new InvalidOperationException("Apple credential natamamdır (Team/PassType/P12/Password tələb olunur).");
            }

            _logger.LogInformation("Apple credential found. TeamId={TeamId}, PassTypeId={PassTypeId}",
                cred.TeamIdentifier, cred.PassTypeIdentifier);

            // P12 yolu – həmişə HTTP(s) / nisbətən URL → walvero.com-dan endirilir
            var p12Path = await ResolvePathOrDownloadAsync(
                cred.AppleCertificateUrl!, "apple-cert", ".p12", ct);
            _logger.LogInformation("Apple P12 resolved. File={FileName}", SafeFileName(p12Path));

            // WWDR – varsa URL-dən, yoxdursa bundled fallback
            string wwdrPath;
            if (!string.IsNullOrWhiteSpace(cred.AppleWwdrCertUrl))
            {
                wwdrPath = await ResolvePathOrDownloadAsync(
                    cred.AppleWwdrCertUrl!, "apple-wwdr", ".cer", ct);
                _logger.LogInformation("WWDR cert resolved from url. File={FileName}", SafeFileName(wwdrPath));
            }
            else
            {
                wwdrPath = await EnsureBundledWwdrAsync(ct);
                _logger.LogInformation("WWDR cert fallback (bundled). File={FileName}", SafeFileName(wwdrPath));
            }

            // Output dir
            var webRoot = string.IsNullOrWhiteSpace(_env.WebRootPath)
                ? Path.Combine(AppContext.BaseDirectory, "wwwroot")
                : _env.WebRootPath;

            var outputDir = Path.Combine(webRoot, "passes");

            _logger.LogInformation("Apple runtime creds ready. OutputDir={Output}", outputDir);

            return new AppleWalletRuntimeCreds
            {
                TeamIdentifier = cred.TeamIdentifier!,
                PassTypeIdentifier = cred.PassTypeIdentifier!,
                CertP12Path = p12Path,
                CertPassword = _secrets.RevealPassword(cred.CertificatePassword), // PAROLU LOG ETMİRİK (enc:v1: → decrypt)
                WwdrCertPath = wwdrPath,
                WebServiceUrl = cred.PassKitServiceUrl,   // TOKEN/URL log etmə!
                AuthToken = cred.PassKitAuthToken,
                OutputDir = outputDir
            };
        }

        public async Task<GoogleWalletRuntimeConfig> GetGoogleAsync(int tenantId, CancellationToken ct = default)
        {
            using var scope = _logger.BeginScope(new Dictionary<string, object?>
            {
                ["TenantId"] = tenantId,
                ["Platform"] = "GooglePay"
            });

            _logger.LogInformation("Fetching Google Wallet credentials...");

            var repo = _uow.Repository<WalletCredential>();
            var cred = repo.GetAll()
                           .FirstOrDefault(x => x.TenantId == tenantId &&
                                                x.Platform == WalletPlatform.GooglePay &&
                                                x.IsActive)
                       ?? throw new InvalidOperationException("Google Wallet credential tapılmadı.");

            if (string.IsNullOrWhiteSpace(cred.IssuerId) ||
                string.IsNullOrWhiteSpace(cred.ServiceAccountEmail) ||
                string.IsNullOrWhiteSpace(cred.GooglePrivateKeyUrl))
            {
                _logger.LogWarning("Google credential incomplete (IssuerId/ServiceAccountEmail/KeyUrl required).");
                throw new InvalidOperationException("Google credential natamamdır (IssuerId/ServiceAccountEmail/KeyUrl tələb olunur).");
            }

            _logger.LogInformation("Google credential found. IssuerId={IssuerId}, ServiceAccount={ServiceAccount}",
                cred.IssuerId, cred.ServiceAccountEmail);

            // Service account JSON – həmişə HTTP(s)/nisbi URL → walvero.com-dan endirilir
            var saJsonPath = await ResolvePathOrDownloadAsync(
                cred.GooglePrivateKeyUrl!, "google-sa", ".json", ct);
            _logger.LogInformation("Google SA JSON resolved. File={FileName}", SafeFileName(saJsonPath));

            var cfg = new GoogleWalletRuntimeConfig
            {
                IssuerId = cred.IssuerId!,
                ServiceAccountJsonPath = saJsonPath,
                ApplicationName = "Walvero Loyalty",
                ClassSuffix = "loyalty",
                Origins = Array.Empty<string>()
            };

            _logger.LogInformation("Google runtime config ready.");
            return cfg;
        }

        // ---------- Helpers ----------

        /// <summary>
        /// Sirr faylının (p12 / WWDR / Google SA JSON) lokal ABSOLUTE yolunu qaytarır.
        /// Öz blob/CDN linkləri storage açarı ilə (authenticated) oxunur; fayl public olmayan temp qovluğa
        /// yazılır və keşlənir (əvvəl hər çağırışda public wwwroot/passes/downloads-a yeni nüsxə yazılırdı).
        /// Nisbi yol → https://appapi.walvero.com/ + yol (legacy).
        /// </summary>
        private async Task<string> ResolvePathOrDownloadAsync(
            string pathOrUrl, string prefix, string defaultExt, CancellationToken ct)
        {
            var source = pathOrUrl.Trim();
            if (!(Uri.TryCreate(source, UriKind.Absolute, out var abs) &&
                  (abs.Scheme == Uri.UriSchemeHttp || abs.Scheme == Uri.UriSchemeHttps)))
            {
                // Məs: "~/resource/Apple.cer", "/resource/Apple.cer", "resource/Apple.cer"
                var rel = source.StartsWith("~/") ? source[2..] : source;
                source = new Uri(WalveroBaseUri, rel.TrimStart('/', '\\')).ToString();
            }

            try
            {
                var localPath = await _secrets.MaterializeAsync(source, defaultExt, ct);
                _logger.LogInformation("Wallet secret resolved. Kind={Kind}, Ext={Ext}", prefix, Path.GetExtension(localPath));
                return localPath;
            }
            catch (Exception ex)
            {
                // URL loglanmır — sirrin yeri təxmin olunmasın
                _logger.LogError(ex, "Wallet secret resolve failed. Kind={Kind}", prefix);
                throw;
            }
        }

        private string ToPublicUrl(string absolutePath)
        {
            var webRoot = string.IsNullOrWhiteSpace(_env.WebRootPath)
                ? Path.Combine(AppContext.BaseDirectory, "wwwroot")
                : _env.WebRootPath;

            var relative = Path.GetRelativePath(webRoot, absolutePath)
                               .Replace('\\', '/'); // URL formatı
            var req = _httpContextAccessor.HttpContext!.Request;
            var basePath = req.PathBase.HasValue ? req.PathBase.Value : string.Empty;

            var publicUrl = $"{req.Scheme}://{req.Host}{basePath}/{relative}";
            _logger.LogDebug("Public URL resolved. File={FileName}, Url={Url}", SafeFileName(absolutePath), publicUrl);
            return publicUrl;
        }

        private async Task<string> EnsureBundledWwdrAsync(CancellationToken ct)
        {
            var webRoot = string.IsNullOrWhiteSpace(_env.WebRootPath)
                ? Path.Combine(AppContext.BaseDirectory, "wwwroot")
                : _env.WebRootPath;

            var bundled = Path.Combine(webRoot, "resource", "AppleWWDRCAG4.cer");

            if (File.Exists(bundled))
            {
                _logger.LogDebug("Bundled WWDR present. File={FileName}", SafeFileName(bundled));
                return bundled;
            }

            _logger.LogWarning("Bundled WWDR not found. Expected={FileName}", SafeFileName(bundled));
            // Yoxdursa yenə də yolu qaytarırıq (istəsən burada exception ata bilərsən)
            return bundled;
        }

        // ---- Small helpers for safe logging (no secrets) ----
        private static string SafeFileName(string path)
        {
            try { return Path.GetFileName(path); } catch { return "<unknown>"; }
        }
    }
}

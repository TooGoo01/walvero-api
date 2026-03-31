using Loyalty.Business.Services.Abstractions.Main;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Loyalty.Business.Services.Implementations.Main
{
    public sealed class FilePathResolver : IFilePathResolver
    {
        private readonly IAppEnvironment _env;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<FilePathResolver> _logger;

        // İstəsən bunu config-dən də oxuya bilərsən
        private static readonly Uri DefaultBaseUri = new("https://appapi.walvero.com/");

        public FilePathResolver(
            IAppEnvironment env,
            IHttpClientFactory httpClientFactory,
            ILogger<FilePathResolver> logger)
        {
            _env = env;
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        public async Task<string> ResolvePathOrDownloadAsync(
            string pathOrUrl,
            string prefix,
            string defaultExt,
            CancellationToken ct = default)
        {
            var webRoot = GetWebRoot();

            try
            {
                // 1) Əgər local absolute path-dirsə → birbaşa yoxla və qaytar
                if (Path.IsPathRooted(pathOrUrl) && !IsHttpUrl(pathOrUrl))
                {
                    if (!File.Exists(pathOrUrl))
                    {
                        _logger.LogWarning("Local file not found. Path={Path}", pathOrUrl);
                        throw new FileNotFoundException("File not found.", pathOrUrl);
                    }

                    _logger.LogInformation("Using existing local file. Path={Path}", pathOrUrl);
                    return pathOrUrl;
                }

                // 2) URL qur: əgər artıq http/https dirsə → birbaşa, yoxsa walvero.com üstündə relative kimi
                var uri = BuildUri(pathOrUrl);

                _logger.LogInformation("Downloading file from URL. Url={Url}", uri);

                var client = _httpClientFactory.CreateClient();
                using var res = await client.GetAsync(uri, ct);
                res.EnsureSuccessStatusCode();

                // 3) Fayl adı / uzantı
                var cd = res.Content.Headers.ContentDisposition;
                string? fileName = cd?.FileNameStar ?? cd?.FileName?.Trim('"');

                var ext = Path.GetExtension(fileName ?? "");
                if (string.IsNullOrWhiteSpace(ext))
                    ext = Path.GetExtension(uri.AbsolutePath);
                if (string.IsNullOrWhiteSpace(ext))
                    ext = defaultExt;

                // 4) WWWROOT/passes/downloads altına yaz
                var targetDir = Path.Combine(webRoot, "passes", "downloads");
                Directory.CreateDirectory(targetDir);

                var localPath = Path.Combine(targetDir, $"{prefix}-{Guid.NewGuid():N}{ext}");

                await using (var fs = new FileStream(localPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    await res.Content.CopyToAsync(fs, ct);
                }

                _logger.LogInformation("Download saved. File={FileName}, Size={SizeBytes}",
                    Path.GetFileName(localPath), new FileInfo(localPath).Length);

                return localPath;
            }
            catch (Exception ex) when (ex is HttpRequestException or IOException or UnauthorizedAccessException)
            {
                _logger.LogError(ex, "ResolvePathOrDownloadAsync failed. Source={Source}", pathOrUrl);
                throw;
            }
        }

        // ----- Helpers -----

        private string GetWebRoot()
        {
            if (!string.IsNullOrWhiteSpace(_env.WebRootPath))
                return _env.WebRootPath!;

            var fallback = Path.Combine(AppContext.BaseDirectory, "wwwroot");
            Directory.CreateDirectory(fallback);
            return fallback;
        }

        private static bool IsHttpUrl(string value)
        {
            return Uri.TryCreate(value, UriKind.Absolute, out var u) &&
                   (u.Scheme == Uri.UriSchemeHttp || u.Scheme == Uri.UriSchemeHttps);
        }

        private static Uri BuildUri(string pathOrUrl)
        {
            // Əgər artıq http/https URL-dirsə
            if (IsHttpUrl(pathOrUrl))
                return new Uri(pathOrUrl);

            // "~/" → kəs
            var rel = pathOrUrl;
            if (rel.StartsWith("~/"))
                rel = rel.Substring(2);

            rel = rel.TrimStart('/', '\\');

            // walvero.com + relative
            return new Uri(DefaultBaseUri, rel);
        }
    }
}

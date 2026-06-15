using Microsoft.Extensions.Configuration;

namespace Loyalty.Business.Services.Implementations.Storage
{
    /// <summary>
    /// Rewrites Azure Blob Storage URLs to point at the CDN endpoint (Azure CDN / Front Door)
    /// when configured. Both new uploads and historical DB rows pass through so backfill is not needed.
    /// Falls back to the original URL when `Cdn:BaseUrl` is empty.
    ///
    /// Example: `https://walverostorage.blob.core.windows.net/photo-menu/x.jpg`
    ///   →     `https://walvero-cdn.azureedge.net/photo-menu/x.jpg`
    /// </summary>
    public sealed class BlobUrlRewriter
    {
        // Hard-coded source host — matches existing AzureStorage URL builder.
        private const string BlobHost = "https://walverostorage.blob.core.windows.net";

        private readonly string? _cdnBaseUrl;

        public BlobUrlRewriter(IConfiguration configuration)
        {
            var raw = configuration["Cdn:BaseUrl"];
            _cdnBaseUrl = string.IsNullOrWhiteSpace(raw) ? null : raw.TrimEnd('/');
        }

        public bool IsEnabled => _cdnBaseUrl is not null;

        public string? Rewrite(string? url)
        {
            if (string.IsNullOrEmpty(url) || _cdnBaseUrl is null) return url;
            if (!url.StartsWith(BlobHost, System.StringComparison.OrdinalIgnoreCase)) return url;
            return _cdnBaseUrl + url.Substring(BlobHost.Length);
        }
    }
}

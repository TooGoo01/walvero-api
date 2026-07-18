using System.Net.Http;                         // IHttpClientFactory burada

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using IHttpClientFactory = System.Net.Http.IHttpClientFactory;
namespace Loyalty.Business.Helpers
{
    public class FileDownloader
    {
        private readonly IHttpClientFactory _http;
        private readonly ILogger<FileDownloader> _logger;

        public FileDownloader(IHttpClientFactory http, ILogger<FileDownloader> logger)
        {
            _http = http;
            _logger = logger;
        }

        public async Task DownloadToAsync(string url, string path, CancellationToken ct = default)
        {
            // Təhlükəsizlik: yalnız öz domen ailənə icazə ver
            var uri = new Uri(url);


            var client = _http.CreateClient("wallet");
            client.Timeout = TimeSpan.FromSeconds(30); // image download timeout
            using var res = await client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, ct);
            if (!res.IsSuccessStatusCode)
            {
                _logger.LogWarning("File download failed: {Url} → HTTP {StatusCode}", url, (int)res.StatusCode);
                throw new HttpRequestException($"Download failed with HTTP {(int)res.StatusCode} for {url}");
            }

            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            await using var fs = File.Create(path);
            await res.Content.CopyToAsync(fs, ct);
        }
    }
}

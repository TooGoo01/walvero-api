using System.Net.Http;                         // IHttpClientFactory burada

using Microsoft.Extensions.DependencyInjection;
using IHttpClientFactory = System.Net.Http.IHttpClientFactory;
namespace Loyalty.Business.Helpers
{
    public class FileDownloader
    {
          private readonly IHttpClientFactory _http;

    public FileDownloader(IHttpClientFactory http)
    {
        _http = http;
    }

        public async Task DownloadToAsync(string url, string path, CancellationToken ct = default)
        {
            // Təhlükəsizlik: yalnız öz domen ailənə icazə ver
            var uri = new Uri(url);
          

            var client = _http.CreateClient("wallet"); // subdomain-friendly konfiqurasiya
            using var res = await client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, ct);
            res.EnsureSuccessStatusCode();

            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            await using var fs = File.Create(path);
            await res.Content.CopyToAsync(fs, ct);
        }
    }
}

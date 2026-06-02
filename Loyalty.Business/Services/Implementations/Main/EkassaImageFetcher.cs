using Loyalty.Business.Services.Abstractions.Main;
using Microsoft.Extensions.Logging;

namespace Loyalty.Business.Services.Implementations.Main
{
    public sealed class EkassaImageFetcher : IEkassaImageFetcher
    {
        public const string HttpClientName = "EkassaImageFetcher";

        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<EkassaImageFetcher> _logger;

        public EkassaImageFetcher(IHttpClientFactory httpClientFactory, ILogger<EkassaImageFetcher> logger)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        public async Task<EkassaImageResult?> FetchAsync(string docId, string urlTemplate, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(docId))
            {
                _logger.LogWarning("EkassaImageFetcher: docId is empty");
                return null;
            }
            if (string.IsNullOrWhiteSpace(urlTemplate) || !urlTemplate.Contains("{docId}"))
            {
                _logger.LogWarning("EkassaImageFetcher: urlTemplate invalid: {Template}", urlTemplate);
                return null;
            }

            var url = urlTemplate.Replace("{docId}", Uri.EscapeDataString(docId));
            _logger.LogInformation("[EKASSA FETCH] start url={Url}", url);

            try
            {
                using var client = _httpClientFactory.CreateClient(HttpClientName);
                using var resp = await client.GetAsync(url, ct);

                _logger.LogInformation("[EKASSA FETCH] status={Status} ct={ContentType} len={Length}",
                    (int)resp.StatusCode, resp.Content.Headers.ContentType?.ToString(), resp.Content.Headers.ContentLength);

                if (!resp.IsSuccessStatusCode)
                {
                    _logger.LogWarning("[EKASSA FETCH] non-success url={Url} status={Status}", url, (int)resp.StatusCode);
                    return null;
                }

                var ct2 = resp.Content.Headers.ContentType?.MediaType ?? "application/octet-stream";
                var bytes = await resp.Content.ReadAsByteArrayAsync(ct);
                if (bytes.Length == 0)
                {
                    _logger.LogWarning("[EKASSA FETCH] empty body url={Url}", url);
                    return null;
                }

                return new EkassaImageResult(bytes, ct2, url);
            }
            catch (TaskCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[EKASSA FETCH] failed url={Url}", url);
                return null;
            }
        }
    }
}

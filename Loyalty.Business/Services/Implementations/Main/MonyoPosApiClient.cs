using System.Net.Http.Json;
using System.Text.Json;
using Loyalty.Business.Services.Abstractions.Main;
using Microsoft.Extensions.Logging;

namespace Loyalty.Business.Services.Implementations.Main
{
    public class MonyoPosApiClient : IMonyoPosApiClient
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<MonyoPosApiClient> _logger;
        private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

        public MonyoPosApiClient(IHttpClientFactory httpClientFactory, ILogger<MonyoPosApiClient> logger)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        public async Task<MonyoCustomerResponse[]?> AddCustomersAsync(string baseUrl, string apiToken, string venueId, MonyoCustomerDto[] customers, CancellationToken ct = default)
        {
            var payload = new[]
            {
                new
                {
                    venue_id = venueId,
                    type = "add",
                    data = customers.Select(c => new
                    {
                        name = c.Name,
                        phone = c.Phone,
                        address = c.Address,
                        birthday = c.Birthday,
                        customer_code = c.CustomerCode,
                        barcode = c.Barcode,
                        discount = c.Discount,
                        balans = c.Balans,
                        note = c.Note
                    }).ToArray()
                }
            };
            return await PostAsync<MonyoCustomerResponse[]>(baseUrl, apiToken, "pos/customers", payload, ct);
        }

        public async Task<MonyoCustomerResponse[]?> UpdateCustomersAsync(string baseUrl, string apiToken, string venueId, MonyoCustomerUpdateDto[] customers, CancellationToken ct = default)
        {
            var payload = new[]
            {
                new
                {
                    venue_id = venueId,
                    type = "update",
                    data = customers.Select(c => new
                    {
                        id = c.MonyoId,
                        name = c.Name,
                        phone = c.Phone,
                        address = c.Address,
                        birthday = c.Birthday,
                        customer_code = c.CustomerCode,
                        barcode = c.Barcode,
                        discount = c.Discount,
                        balans = c.Balans,
                        note = c.Note
                    }).ToArray()
                }
            };
            return await PostAsync<MonyoCustomerResponse[]>(baseUrl, apiToken, "pos/customers", payload, ct);
        }

        private async Task<T?> PostAsync<T>(string baseUrl, string apiToken, string path, object payload, CancellationToken ct)
        {
            var url = baseUrl.TrimEnd('/') + "/" + path.TrimStart('/');
            var client = _httpClientFactory.CreateClient();
            using var request = new HttpRequestMessage(HttpMethod.Post, url);
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiToken);
            request.Content = JsonContent.Create(payload, options: JsonOptions);

            var response = await client.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Monyo API {Path} returned {Status}", path, response.StatusCode);
                return default;
            }
            return await response.Content.ReadFromJsonAsync<T>(JsonOptions, ct);
        }
    }
}

using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Loyalty.Business.Services.Abstractions.Main;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace Loyalty.Business.Services.Implementations.Main
{
    public class IikoPosApiClient : IIikoPosApiClient
    {
        private const string BaseUrl = "https://api-ru.iiko.services";
        private static readonly TimeSpan TokenCacheDuration = TimeSpan.FromMinutes(55);

        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IMemoryCache _cache;
        private readonly ILogger<IikoPosApiClient> _logger;

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        public IikoPosApiClient(
            IHttpClientFactory httpClientFactory,
            IMemoryCache cache,
            ILogger<IikoPosApiClient> logger)
        {
            _httpClientFactory = httpClientFactory;
            _cache = cache;
            _logger = logger;
        }

        public async Task<string?> GetAccessTokenAsync(string apiLogin, CancellationToken ct = default)
        {
            var cacheKey = $"iiko_token_{apiLogin}";
            if (_cache.TryGetValue(cacheKey, out string? cachedToken))
                return cachedToken;

            // iiko auth v2 — body {apiLogin} → {correlationId, token} (Token oxunur).
            var response = await PostAsync<IikoTokenResponse>(
                null, "/api/v2/access_token", new { apiLogin }, ct);

            if (response?.Token == null)
                return null;

            _cache.Set(cacheKey, response.Token, TokenCacheDuration);
            return response.Token;
        }

        public async Task<IikoCustomerResponse?> CreateOrUpdateCustomerAsync(
            string accessToken, Guid organizationId, IikoCustomerDto customer, CancellationToken ct = default)
        {
            var payload = new
            {
                organizationId,
                id = customer.Id,
                name = customer.Name,
                surName = customer.Surname,
                phone = customer.Phone,
                email = customer.Email,
                cardTrack = customer.CardTrack,
                cardNumber = customer.CardNumber,
                birthday = customer.Birthday
            };

            return await PostAsync<IikoCustomerResponse>(
                accessToken, "/api/1/loyalty/iiko/customer/create_or_update", payload, ct);
        }

        public async Task<IikoCustomerInfoResponse?> GetCustomerInfoAsync(
            string accessToken, Guid organizationId, string? phone = null, string? cardTrack = null, CancellationToken ct = default)
        {
            var payload = new
            {
                organizationId,
                type = phone != null ? "phone" : "cardTrack",
                phone,
                cardTrack
            };

            return await PostAsync<IikoCustomerInfoResponse>(
                accessToken, "/api/1/loyalty/iiko/customer/info", payload, ct);
        }

        public async Task<bool> AddCustomerCardAsync(
            string accessToken, Guid organizationId, Guid customerId, string cardTrack, string cardNumber, CancellationToken ct = default)
        {
            var payload = new
            {
                organizationId,
                customerId,
                cardTrack,
                cardNumber
            };

            var result = await PostRawAsync(accessToken, "/api/1/loyalty/iiko/customer/card/add", payload, ct);
            return result;
        }

        public async Task<bool> AddCustomerToProgramAsync(
            string accessToken, Guid organizationId, Guid customerId, Guid programId, CancellationToken ct = default)
        {
            var payload = new
            {
                organizationId,
                customerId,
                programId
            };

            return await PostRawAsync(accessToken, "/api/1/loyalty/iiko/customer/program/add", payload, ct);
        }

        public async Task<bool> TopUpWalletAsync(
            string accessToken, Guid organizationId, Guid customerId, Guid walletId, decimal amount, CancellationToken ct = default)
        {
            var payload = new
            {
                organizationId,
                customerId,
                walletId,
                sum = amount
            };

            return await PostRawAsync(accessToken, "/api/1/loyalty/iiko/customer/wallet/topup", payload, ct);
        }

        public async Task<IikoOrganization[]?> GetOrganizationsAsync(string accessToken, CancellationToken ct = default)
        {
            var response = await PostAsync<IikoOrganizationsResponse>(
                accessToken, "/api/1/organizations", new { }, ct);

            return response?.Organizations;
        }

        public async Task<IikoNomenclatureResponse?> GetNomenclatureAsync(
            string accessToken, Guid organizationId, CancellationToken ct = default)
        {
            return await PostAsync<IikoNomenclatureResponse>(
                accessToken, "/api/1/nomenclature", new { organizationId }, ct);
        }

        // ── Private helpers ──

        private async Task<T?> PostAsync<T>(string? accessToken, string path, object payload, CancellationToken ct)
        {
            var url = BaseUrl + path;
            var client = _httpClientFactory.CreateClient();
            using var request = new HttpRequestMessage(HttpMethod.Post, url);

            if (!string.IsNullOrEmpty(accessToken))
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

            request.Content = JsonContent.Create(payload, options: JsonOptions);

            HttpResponseMessage response;
            try
            {
                response = await client.SendAsync(request, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "iiko API request to {Path} failed", path);
                return default;
            }

            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(ct);
                _logger.LogWarning("iiko API {Path} returned {Status}: {Body}", path, response.StatusCode, body);
                return default;
            }

            return await response.Content.ReadFromJsonAsync<T>(JsonOptions, ct);
        }

        private async Task<bool> PostRawAsync(string? accessToken, string path, object payload, CancellationToken ct)
        {
            var url = BaseUrl + path;
            var client = _httpClientFactory.CreateClient();
            using var request = new HttpRequestMessage(HttpMethod.Post, url);

            if (!string.IsNullOrEmpty(accessToken))
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

            request.Content = JsonContent.Create(payload, options: JsonOptions);

            try
            {
                var response = await client.SendAsync(request, ct);
                if (!response.IsSuccessStatusCode)
                {
                    var body = await response.Content.ReadAsStringAsync(ct);
                    _logger.LogWarning("iiko API {Path} returned {Status}: {Body}", path, response.StatusCode, body);
                }
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "iiko API request to {Path} failed", path);
                return false;
            }
        }

        private class IikoTokenResponse
        {
            public string? Token { get; set; }
        }

        private class IikoOrganizationsResponse
        {
            public IikoOrganization[]? Organizations { get; set; }
        }
    }
}

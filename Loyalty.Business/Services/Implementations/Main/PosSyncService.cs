using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Loyalty.Business.Services.Abstractions.Main;
using Loyalty.DataAccess.Context;
using Loyalty.DataAccess.Entities.Main;
using Loyalty.DataAccess.Repositories.Abstractions.Main;
using Loyalty.DataAccess.UnitOfWorks;
using Loyalty.Infrastructure.Utilities.Security.Encryption;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Loyalty.Business.Services.Implementations.Main
{
    public class PosSyncService : IPosSyncService
    {
        private readonly ITenantIntegrationRepository _integrationRepo;
        private readonly IPosCustomerMappingRepository _mappingRepo;
        private readonly ILoyaltyCardRepository _cardRepo;
        private readonly ISensitiveDataProtector _protector;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IUnitOfWork _uow;
        private readonly LoyaltyWalletDbContext _db;
        private readonly ILogger<PosSyncService> _logger;

        private static readonly JsonSerializerOptions _jsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        public PosSyncService(
            ITenantIntegrationRepository integrationRepo,
            IPosCustomerMappingRepository mappingRepo,
            ILoyaltyCardRepository cardRepo,
            ISensitiveDataProtector protector,
            IHttpClientFactory httpClientFactory,
            IUnitOfWork uow,
            LoyaltyWalletDbContext db,
            ILogger<PosSyncService> logger)
        {
            _integrationRepo = integrationRepo;
            _mappingRepo = mappingRepo;
            _cardRepo = cardRepo;
            _protector = protector;
            _httpClientFactory = httpClientFactory;
            _uow = uow;
            _db = db;
            _logger = logger;
        }

        public async Task SyncCustomerToPosAsync(int tenantId, Customer customer, CancellationToken ct = default)
        {
            var integrations = await _integrationRepo.GetAllActiveByTenantAsync(tenantId, ct);
            if (integrations == null || !integrations.Any())
                return;

            foreach (var integration in integrations)
            {
                if (string.IsNullOrWhiteSpace(integration.ApiBaseUrl))
                    continue;

                try
                {
                    // Skip if already mapped
                    var existing = await _mappingRepo.GetByTenantIntegrationAndCustomerAsync(integration.Id, customer.Id, ct);
                    if (existing != null)
                    {
                        _logger.LogDebug("POS sync skipped (already mapped): IntegrationId={IntegrationId}, CustomerId={CustomerId}", integration.Id, customer.Id);
                        continue;
                    }

                    var externalId = await SyncToSinglePosAsync(integration, customer, ct);

                    // POS external ID qaytarmasa, öz customer ID-ni istifadə et
                    externalId ??= customer.Id.ToString();

                    if (!string.IsNullOrEmpty(externalId))
                    {
                        // Save mapping
                        var mapping = new PosCustomerMapping
                        {
                            TenantIntegrationId = integration.Id,
                            ExternalCustomerId = externalId,
                            CustomerId = customer.Id,
                            RegUserId = 1,
                            RegDate = DateTime.UtcNow,
                            IsActive = true
                        };
                        await _mappingRepo.AddAsync(mapping);
                        await _uow.CommitAsync(ct);

                        _logger.LogInformation(
                            "POS sync OK: IntegrationId={IntegrationId}, CustomerId={CustomerId}, ExternalId={ExternalId}",
                            integration.Id, customer.Id, externalId);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex,
                        "POS sync FAILED: IntegrationId={IntegrationId}, CustomerId={CustomerId}",
                        integration.Id, customer.Id);
                }
            }
        }

        private async Task<string?> SyncToSinglePosAsync(TenantIntegration integration, Customer customer, CancellationToken ct)
        {
            var rawBaseUrl = integration.ApiBaseUrl!.TrimEnd('/');
            var hasUrlCredentials = rawBaseUrl.Contains("@");

            HttpClient client;
            string url;

            if (hasUrlCredentials)
            {
                // URL has credentials (e.g. http://1C:987654321@host/path)
                // Use handler that does NOT auto-send credentials as Auth header
                var handler = new HttpClientHandler
                {
                    Credentials = null,
                    PreAuthenticate = false,
                    UseDefaultCredentials = false
                };
                client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(15) };
                url = $"{rawBaseUrl}/customers";
                // No Authorization header - credentials stay in URL only
            }
            else
            {
                client = _httpClientFactory.CreateClient();
                client.Timeout = TimeSpan.FromSeconds(15);
                url = $"{rawBaseUrl}/customers";

                // ApiUsername + ApiToken → Basic Auth
                if (!string.IsNullOrEmpty(integration.ApiUsername) && !string.IsNullOrEmpty(integration.ApiTokenEncrypted))
                {
                    var password = _protector.Decrypt(integration.ApiTokenEncrypted);
                    var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{integration.ApiUsername}:{password}"));
                    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", credentials);
                }
                // ApiToken only → Bearer
                else if (!string.IsNullOrEmpty(integration.ApiTokenEncrypted))
                {
                    var token = _protector.Decrypt(integration.ApiTokenEncrypted);
                    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
                }
            }

            var name = $"{customer.FirstName} {customer.LastName}".Trim();
            if (string.IsNullOrEmpty(name)) name = customer.PhoneNumber;

            // BarcodeValue-nu LoyaltyCard-dan al
            var cards = await _cardRepo.ListByCustomerAsync(integration.TenantId, customer.Id, ct);
            var barcodeValue = cards?
                .Where(c => c.IsActive && !string.IsNullOrEmpty(c.BarcodeValue))
                .OrderByDescending(c => c.Id)
                .Select(c => c.BarcodeValue)
                .FirstOrDefault();

            // WalveroId-ni ApplicationUser-dən al
            long? walveroId = null;
            if (customer.applicationUserId.HasValue)
            {
                walveroId = await _db.Users
                    .Where(u => u.Id == customer.applicationUserId.Value)
                    .Select(u => u.WalveroId)
                    .FirstOrDefaultAsync(ct);
            }

            // Barcode: "walveroId,barcodeValue" formatında
            string barcode;
            if (walveroId.HasValue && !string.IsNullOrEmpty(barcodeValue))
                barcode = $"{walveroId.Value},{barcodeValue}";
            else if (walveroId.HasValue)
                barcode = walveroId.Value.ToString();
            else if (!string.IsNullOrEmpty(barcodeValue))
                barcode = barcodeValue;
            else
                barcode = customer.Id.ToString();

            var barcode1 = walveroId?.ToString() ?? "";
            var barcode2 = barcodeValue ?? "";

            var body = new[]
            {
                new
                {
                    venue_id = integration.ExternalVenueId ?? "1",
                    type = "add",
                    data = new[]
                    {
                        new
                        {
                            name = name,
                            customer_code = customer.Id.ToString(),
                            barcode = barcode,
                            barcode1 = barcode1,
                            barcode2 = barcode2
                        }
                    }
                }
            };

            var json = JsonSerializer.Serialize(body);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            _logger.LogDebug("POS sync request: Url={Url}, Body={Body}", url, json);

            var response = await client.PostAsync(url, content, ct);
            var responseBody = await response.Content.ReadAsStringAsync(ct);

            _logger.LogDebug("POS sync response: Status={StatusCode}, Body={Body}", response.StatusCode, responseBody);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("POS sync HTTP error: Status={StatusCode}, Body={Body}, Url={Url}",
                    response.StatusCode, responseBody, url);
                return null;
            }

            // Try to extract external customer ID from response
            return ExtractExternalId(responseBody);
        }

        /// <summary>
        /// POS response-dan external customer ID-ni extract edir.
        /// Ferqli POS formatlarini destekleyir:
        /// - {"id": "123"} veya {"id": 123}
        /// - {"customerId": "123"}
        /// - {"data": {"id": "123"}}
        /// - {"customers": [{"id": "123"}]}
        /// - [{"id": "123"}]
        /// </summary>
        private string? ExtractExternalId(string responseBody)
        {
            if (string.IsNullOrWhiteSpace(responseBody))
                return null;

            try
            {
                using var doc = JsonDocument.Parse(responseBody);
                var root = doc.RootElement;

                // Array response: [{"id": "123"}]
                if (root.ValueKind == JsonValueKind.Array && root.GetArrayLength() > 0)
                {
                    return TryGetId(root[0]);
                }

                // Object response
                if (root.ValueKind == JsonValueKind.Object)
                {
                    // Direct: {"id": "123"} or {"customerId": "123"}
                    var id = TryGetId(root);
                    if (id != null) return id;

                    // Nested: {"data": {"id": "123"}}
                    if (root.TryGetProperty("data", out var data))
                    {
                        if (data.ValueKind == JsonValueKind.Object)
                            return TryGetId(data);
                        if (data.ValueKind == JsonValueKind.Array && data.GetArrayLength() > 0)
                            return TryGetId(data[0]);
                    }

                    // Array field: {"customers": [{"id": "123"}]}
                    if (root.TryGetProperty("customers", out var customers) &&
                        customers.ValueKind == JsonValueKind.Array && customers.GetArrayLength() > 0)
                    {
                        return TryGetId(customers[0]);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Failed to parse POS response for external ID");
            }

            return null;
        }

        private static string? TryGetId(JsonElement el)
        {
            foreach (var prop in new[] { "id", "Id", "ID", "customerId", "CustomerId", "customer_id" })
            {
                if (el.TryGetProperty(prop, out var val))
                {
                    return val.ValueKind switch
                    {
                        JsonValueKind.String => val.GetString(),
                        JsonValueKind.Number => val.GetRawText(),
                        _ => null
                    };
                }
            }
            return null;
        }
    }
}

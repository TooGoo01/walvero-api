using System.Text;
using System.Text.Json.Nodes;
using Loyalty.Business.Results;
using Loyalty.Business.Services.Abstractions.Main;
using Loyalty.DataAccess.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Loyalty.Business.Services.Implementations.Main
{
    /// <summary>
    /// Tenant üçün AI (Azure OpenAI) ilə axtarış açar sözləri generasiya edir.
    /// Giriş: ad + kateqoriya + təsvir + menyu yeməkləri. Nəticə: vergüllə açar sözlər.
    /// Chatbot ilə eyni Azure config/HTTP (gpt-5 uyğun params, endpoint normalize).
    /// </summary>
    public class KeywordGenerationService : IKeywordGenerationService
    {
        private readonly IConfiguration _config;
        private readonly IHttpClientFactory _httpFactory;
        private readonly LoyaltyWalletDbContext _db;
        private readonly ILogger<KeywordGenerationService> _logger;

        public KeywordGenerationService(
            IConfiguration config,
            IHttpClientFactory httpFactory,
            LoyaltyWalletDbContext db,
            ILogger<KeywordGenerationService> logger)
        {
            _config = config;
            _httpFactory = httpFactory;
            _db = db;
            _logger = logger;
        }

        public async Task<ServiceResult> GenerateForTenantAsync(int tenantId, CancellationToken ct = default)
        {
            var endpoint = Pick("AzureOpenAI:Endpoint", "AZURE_OPENAI_ENDPOINT");
            var apiKey = Pick("AzureOpenAI:ApiKey", "AZURE_OPENAI_API_KEY");
            var deployment = Pick("AzureOpenAI:Deployment", "AZURE_OPENAI_DEPLOYMENT", "AZURE_OPENAI_DEPLOYMENT_NAME");
            var apiVersion = Pick("AzureOpenAI:ApiVersion", "AZURE_OPENAI_API_VERSION") ?? "2025-04-01-preview";

            if (string.IsNullOrWhiteSpace(endpoint) || string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(deployment))
                return new ServiceResult(false, "AI konfiqurasiyası yoxdur (Azure OpenAI).");

            if (Uri.TryCreate(endpoint, UriKind.Absolute, out var epUri))
                endpoint = $"{epUri.Scheme}://{epUri.Authority}";

            var tenant = await _db.Tenants
                .Include(t => t.Category)
                .Include(t => t.Branding)
                .FirstOrDefaultAsync(t => t.Id == tenantId, ct);
            if (tenant == null)
                return new ServiceResult(false, "Tenant tapılmadı");

            // Menyu yeməkləri (effective tenant = MenuItem.TenantId ?? MenuCategory.TenantId)
            var dishes = await _db.MenuItems
                .AsNoTracking()
                .Include(m => m.MenuCategory)
                .Where(m => m.IsActive && (m.TenantId ?? m.MenuCategory.TenantId) == tenantId)
                .OrderBy(m => m.SortOrder)
                .Select(m => m.AzName)
                .Take(50)
                .ToListAsync(ct);

            var category = tenant.Category?.AzName ?? tenant.Category?.EnName;
            var description = tenant.Branding?.Description;
            var existing = tenant.Keywords;

            var sb = new StringBuilder();
            sb.AppendLine("Sən Azərbaycan (Bakı) bazarı üçün məkan-axtarış açar sözləri yaradan köməkçisən.");
            sb.AppendLine("Aşağıdakı məkan üçün 15-25 qısa açar söz təklif et: mətbəx növləri, populyar yeməklər,");
            sb.AppendLine("xüsusiyyətlər (qəlyan, wifi, çatdırılma, terras və s.), ərazi/məhəllə (əgər bilinirsə).");
            sb.AppendLine("YALNIZ açar sözləri vergüllə ayırıb qaytar — izah, nömrələmə, başqa mətn YOX.");
            sb.AppendLine($"Məkan adı: {tenant.CompanyName}");
            if (!string.IsNullOrWhiteSpace(category)) sb.AppendLine($"Kateqoriya: {category}");
            if (!string.IsNullOrWhiteSpace(description)) sb.AppendLine($"Təsvir: {description}");
            if (dishes.Count > 0) sb.AppendLine($"Menyu yeməkləri: {string.Join(", ", dishes)}");
            if (!string.IsNullOrWhiteSpace(existing)) sb.AppendLine($"Mövcud açar sözlər (təkrar etmə, tamamla): {existing}");

            var body = new JsonObject
            {
                ["messages"] = new JsonArray
                {
                    new JsonObject { ["role"] = "system", ["content"] = "Sən dəqiq, qısa açar söz generatoru-san." },
                    new JsonObject { ["role"] = "user", ["content"] = sb.ToString() }
                },
                ["max_completion_tokens"] = 400
            };

            var url = $"{endpoint.TrimEnd('/')}/openai/deployments/{deployment}/chat/completions?api-version={apiVersion}";
            var client = _httpFactory.CreateClient(AzureOpenAiPlaceAssistantService.HttpClientName);

            using var req = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json")
            };
            req.Headers.Add("api-key", apiKey);

            using var resp = await client.SendAsync(req, ct);
            var json = await resp.Content.ReadAsStringAsync(ct);
            if (!resp.IsSuccessStatusCode)
            {
                _logger.LogError("Keyword gen AzureOpenAI error {Status}: {Body}", (int)resp.StatusCode, json);
                return new ServiceResult(false, "AI generasiyası alınmadı, bir azdan yenidən yoxlayın.");
            }

            var content = JsonNode.Parse(json)?["choices"]?[0]?["message"]?["content"]?.GetValue<string>() ?? string.Empty;

            // Vergül/sətir/nöqtəli vergüllə ayır, təmizlə, təkrarları at.
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var keywords = content
                .Split(new[] { ',', '\n', ';' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(k => k.Trim().TrimStart('-', '•', '*').Trim())
                .Where(k => k.Length is > 1 and <= 40 && seen.Add(k))
                .ToList();

            return new ServiceResult(true, new { tenantId, keywords = string.Join(", ", keywords) });
        }

        private string? Pick(params string[] keys)
        {
            foreach (var k in keys)
            {
                var v = _config[k];
                if (!string.IsNullOrWhiteSpace(v)) return v.Trim();
            }
            return null;
        }
    }
}

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
        private readonly IAzureOpenAiConfigProvider _aiConfig;
        private readonly IHttpClientFactory _httpFactory;
        private readonly LoyaltyWalletDbContext _db;
        private readonly ILogger<KeywordGenerationService> _logger;

        public KeywordGenerationService(
            IAzureOpenAiConfigProvider aiConfig,
            IHttpClientFactory httpFactory,
            LoyaltyWalletDbContext db,
            ILogger<KeywordGenerationService> logger)
        {
            _aiConfig = aiConfig;
            _httpFactory = httpFactory;
            _db = db;
            _logger = logger;
        }

        public async Task<ServiceResult> GenerateForTenantAsync(int tenantId, CancellationToken ct = default)
        {
            var ai = await _aiConfig.GetAsync(ct);
            var endpoint = ai.Endpoint;
            var apiKey = ai.ApiKey;
            var deployment = ai.Deployment;
            var apiVersion = ai.ApiVersion ?? "2025-04-01-preview";

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
            sb.AppendLine("You generate place-search keywords for the Azerbaijan (Baku) market.");
            sb.AppendLine("For the place below, propose 15-25 short keywords: cuisine types, popular dishes,");
            sb.AppendLine("features (hookah, wifi, delivery, terrace, etc.), area/neighborhood (if known).");
            sb.AppendLine("Output the keywords in Azerbaijani. Return ONLY the keywords, comma-separated — no");
            sb.AppendLine("explanation, numbering, or other text.");
            sb.AppendLine($"Place name: {tenant.CompanyName}");
            if (!string.IsNullOrWhiteSpace(category)) sb.AppendLine($"Category: {category}");
            if (!string.IsNullOrWhiteSpace(description)) sb.AppendLine($"Description: {description}");
            if (dishes.Count > 0) sb.AppendLine($"Menu dishes: {string.Join(", ", dishes)}");
            if (!string.IsNullOrWhiteSpace(existing)) sb.AppendLine($"Existing keywords (do not repeat, complement): {existing}");

            var body = new JsonObject
            {
                ["messages"] = new JsonArray
                {
                    new JsonObject { ["role"] = "system", ["content"] = "You are a precise, concise keyword generator." },
                    new JsonObject { ["role"] = "user", ["content"] = sb.ToString() }
                },
                // gpt-5/reasoning modelləri reasoning-ə token xərcləyir → output üçün geniş limit.
                ["max_completion_tokens"] = 2000
            };
            // Effort konfiqurasiyadan (gpt-5.6 "minimal" qəbul etmir: none/low/...); yoxdursa köhnə default, "omit" → göndərmə.
            var reasoningEffort = ai.ReasoningEffort ?? "minimal";
            if (!string.Equals(reasoningEffort, "omit", StringComparison.OrdinalIgnoreCase))
                body["reasoning_effort"] = reasoningEffort;

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

            if (keywords.Count == 0)
                return new ServiceResult(false, "AI boş nəticə qaytardı, bir daha cəhd et.");

            return new ServiceResult(true, new { tenantId, keywords = string.Join(", ", keywords) });
        }

    }
}

using Loyalty.Business.Helpers;
using Loyalty.DataAccess.Context;
using Loyalty.Infrastructure.Utilities.Security.Encryption;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Loyalty.Business.Services.Implementations.Main
{
    /// <summary>Azure OpenAI bağlantı ayarları (chatbot, keyword generasiyası, çek OCR — hamısı eyni mənbədən).</summary>
    public sealed record AzureOpenAiConfig(string? Endpoint, string? ApiKey, string? Deployment, string? ApiVersion, string? ReasoningEffort, string Source)
    {
        public bool IsConfigured => !string.IsNullOrWhiteSpace(Endpoint) && !string.IsNullOrWhiteSpace(ApiKey) && !string.IsNullOrWhiteSpace(Deployment);
    }

    public interface IAzureOpenAiConfigProvider
    {
        Task<AzureOpenAiConfig> GetAsync(CancellationToken ct = default);
        void Invalidate();
    }

    /// <summary>
    /// Sıra: DB `AppSettings` (açarlar <see cref="Keys"/>; ApiKey `enc:v1:` şifrəli ola bilər) → IConfiguration
    /// (`AzureOpenAI:X` / flat `AZURE_OPENAI_X`). DB üstündür ki, App Service env-ə toxunmadan resurs dəyişdirilə bilsin
    /// və ayar DB ilə birgə köçsün. 60 s cache. Singleton.
    /// </summary>
    public sealed class AzureOpenAiConfigProvider : IAzureOpenAiConfigProvider
    {
        public static class Keys
        {
            public const string Endpoint = "ai.openai.endpoint";
            public const string ApiKey = "ai.openai.apiKey";
            public const string Deployment = "ai.openai.deployment";
            public const string ApiVersion = "ai.openai.apiVersion";
            public const string ReasoningEffort = "ai.openai.reasoningEffort";
            public static readonly string[] All = { Endpoint, ApiKey, Deployment, ApiVersion, ReasoningEffort };
        }

        private const string CacheKey = "ai:openai:config";
        private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(60);

        private readonly IServiceScopeFactory _scopes;
        private readonly IConfiguration _config;
        private readonly IMemoryCache _cache;
        private readonly ILogger<AzureOpenAiConfigProvider> _logger;
        private readonly Lazy<SensitiveDataProtector> _protector;

        public AzureOpenAiConfigProvider(IServiceScopeFactory scopes, IConfiguration config, IMemoryCache cache, ILogger<AzureOpenAiConfigProvider> logger)
        {
            _scopes = scopes;
            _config = config;
            _cache = cache;
            _logger = logger;
            _protector = new Lazy<SensitiveDataProtector>(() => new SensitiveDataProtector(_config));
        }

        public void Invalidate() => _cache.Remove(CacheKey);

        public async Task<AzureOpenAiConfig> GetAsync(CancellationToken ct = default)
        {
            if (_cache.TryGetValue(CacheKey, out AzureOpenAiConfig? cached) && cached is not null)
                return cached;

            var db = new Dictionary<string, string>(StringComparer.Ordinal);
            try
            {
                using var scope = _scopes.CreateScope();
                var ctx = scope.ServiceProvider.GetRequiredService<LoyaltyWalletDbContext>();
                db = await ctx.AppSettings.AsNoTracking()
                    .Where(s => Keys.All.Contains(s.Key))
                    .ToDictionaryAsync(s => s.Key, s => s.Value ?? string.Empty, ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "AzureOpenAI DB settings read failed; falling back to configuration.");
            }

            var cfg = Resolve(db, k => _config[k], c => _protector.Value.Decrypt(c),
                ex => _logger.LogWarning(ex, "AzureOpenAI DB api key could not be decrypted."));
            _cache.Set(CacheKey, cfg, CacheTtl);
            return cfg;
        }

        /// <summary>
        /// Pure: DB dəyərləri + config oxuyucu → yekun ayar. Endpoint+ApiKey cütü ya tam DB-dən, ya tam config-dən
        /// (yarımçıq DB ayarı köhnə resursun açarı ilə qarışmasın); DB tam olanda Deployment/ApiVersion/ReasoningEffort da DB-dən (yoxdursa config).
        /// ApiVersion/ReasoningEffort null qala bilər — hər servis öz default-unu qoyur.
        /// </summary>
        public static AzureOpenAiConfig Resolve(
            IReadOnlyDictionary<string, string> db, Func<string, string?> config, Func<string, string?> decrypt, Action<Exception>? onDecryptError = null)
        {
            string? FromDb(string key) => db.TryGetValue(key, out var v) && !string.IsNullOrWhiteSpace(v) ? v.Trim() : null;
            string? FromCfg(params string[] keys)
            {
                foreach (var k in keys)
                {
                    var v = config(k);
                    if (!string.IsNullOrWhiteSpace(v)) return v.Trim();
                }
                return null;
            }

            var dbEndpoint = FromDb(Keys.Endpoint);
            var dbKeyRaw = FromDb(Keys.ApiKey);
            string? dbKey = null;
            if (dbKeyRaw != null)
            {
                try { dbKey = WalletSecretPaths.Reveal(dbKeyRaw, decrypt); }
                catch (Exception ex) { onDecryptError?.Invoke(ex); }
            }
            var useDb = dbEndpoint != null && !string.IsNullOrWhiteSpace(dbKey);

            var endpoint = useDb ? dbEndpoint : FromCfg("AzureOpenAI:Endpoint", "AZURE_OPENAI_ENDPOINT");
            var apiKey = useDb ? dbKey : FromCfg("AzureOpenAI:ApiKey", "AZURE_OPENAI_API_KEY");
            // DB ayarı yalnız endpoint+açar da DB-dədirsə tətbiq olunur (yeni deployment adı köhnə resursa getməsin)
            string? Db(string key) => useDb ? FromDb(key) : null;
            var deployment = Db(Keys.Deployment) ?? FromCfg("AzureOpenAI:Deployment", "AZURE_OPENAI_DEPLOYMENT", "AZURE_OPENAI_DEPLOYMENT_NAME");
            var apiVersion = Db(Keys.ApiVersion) ?? FromCfg("AzureOpenAI:ApiVersion", "AZURE_OPENAI_API_VERSION");
            var effort = Db(Keys.ReasoningEffort) ?? FromCfg("AzureOpenAI:ReasoningEffort", "AZURE_OPENAI_REASONING_EFFORT");

            // Baza host-a normalize — tam URL (path/query ilə) yapışdırılsa belə `scheme://host`
            if (!string.IsNullOrWhiteSpace(endpoint) && Uri.TryCreate(endpoint, UriKind.Absolute, out var u))
                endpoint = $"{u.Scheme}://{u.Authority}";

            return new AzureOpenAiConfig(endpoint, apiKey, deployment, apiVersion, effort, useDb ? "db" : "config");
        }
    }
}

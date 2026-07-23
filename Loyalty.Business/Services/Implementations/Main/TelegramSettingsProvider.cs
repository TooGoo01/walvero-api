using Loyalty.Business.Services.Abstractions.Main;
using Loyalty.DataAccess.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;

namespace Loyalty.Business.Services.Implementations.Main
{
    // DB (AppSettings) → IConfiguration fallback. Super admin paneldən dəyişdirilə bilir,
    // ona görə dəyərlər konstruktor vaxtı yox, sorğu vaxtı oxunur (qısa cache ilə).
    public class TelegramSettingsProvider : ITelegramSettingsProvider
    {
        private const string CacheKey = "tg-settings";
        private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(1);

        private static readonly string[] Keys =
        {
            TelegramSettingKeys.BotToken,
            TelegramSettingKeys.BotUsername,
            TelegramSettingKeys.WebhookSecret
        };

        private readonly LoyaltyWalletDbContext _db;
        private readonly IConfiguration _configuration;
        private readonly IMemoryCache _cache;

        public TelegramSettingsProvider(
            LoyaltyWalletDbContext db,
            IConfiguration configuration,
            IMemoryCache cache)
        {
            _db = db;
            _configuration = configuration;
            _cache = cache;
        }

        public async Task<TelegramSettings> GetAsync(CancellationToken ct = default)
        {
            if (_cache.TryGetValue<TelegramSettings>(CacheKey, out var cached) && cached != null)
                return cached;

            var dbValues = await _db.AppSettings.AsNoTracking()
                .Where(s => Keys.Contains(s.Key))
                .ToDictionaryAsync(s => s.Key, s => s.Value, ct);

            string Resolve(string key) =>
                dbValues.TryGetValue(key, out var v) && !string.IsNullOrWhiteSpace(v)
                    ? v.Trim()
                    : _configuration[key] ?? string.Empty;

            var settings = new TelegramSettings(
                Resolve(TelegramSettingKeys.BotToken),
                Resolve(TelegramSettingKeys.BotUsername),
                Resolve(TelegramSettingKeys.WebhookSecret));

            _cache.Set(CacheKey, settings, CacheTtl);
            return settings;
        }

        public void Invalidate() => _cache.Remove(CacheKey);
    }
}

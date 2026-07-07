using Loyalty.Business.Services.Abstractions.Main;
using Loyalty.DataAccess.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace Loyalty.Business.Services.Implementations.Main
{
    public class TenantStatusGuard : ITenantStatusGuard
    {
        /// <summary>
        /// Son istifadəçiyə/kassirə göstərilən mesaj — borc/deaktivasiya detalını açmır.
        /// </summary>
        public const string InactiveMessage =
            "Xidmət müvəqqəti dayandırılıb. Zəhmət olmasa provayderlə əlaqə saxlayın.";

        private static readonly TimeSpan _ttl = TimeSpan.FromSeconds(60);

        private readonly LoyaltyWalletDbContext _dbContext;
        private readonly IMemoryCache _cache;

        public TenantStatusGuard(LoyaltyWalletDbContext dbContext, IMemoryCache cache)
        {
            _dbContext = dbContext;
            _cache = cache;
        }

        /// <summary>SetActive zamanı cache-i dərhal təmizləmək üçün açar.</summary>
        public static string CacheKey(int tenantId) => $"tenant:isactive:{tenantId}";

        public async Task<bool> IsActiveAsync(int tenantId, CancellationToken ct = default)
        {
            if (tenantId <= 0) return false;

            if (_cache.TryGetValue<bool>(CacheKey(tenantId), out var cached))
                return cached;

            var isActive = await _dbContext.Tenants
                .AsNoTracking()
                .Where(t => t.Id == tenantId)
                .Select(t => (bool?)t.IsActive)
                .FirstOrDefaultAsync(ct) ?? false;

            _cache.Set(CacheKey(tenantId), isActive, _ttl);
            return isActive;
        }
    }
}

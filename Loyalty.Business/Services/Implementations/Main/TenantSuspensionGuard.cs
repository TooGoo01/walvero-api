using Loyalty.Business.Services.Abstractions.Main;
using Loyalty.DataAccess.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace Loyalty.Business.Services.Implementations.Main
{
    public class TenantSuspensionGuard : ITenantSuspensionGuard
    {
        private static readonly TimeSpan _ttl = TimeSpan.FromSeconds(60);

        private readonly LoyaltyWalletDbContext _dbContext;
        private readonly IMemoryCache _cache;

        public TenantSuspensionGuard(LoyaltyWalletDbContext dbContext, IMemoryCache cache)
        {
            _dbContext = dbContext;
            _cache = cache;
        }

        /// <summary>SetServiceSuspended zamanı cache-i dərhal təmizləmək üçün açar.</summary>
        public static string CacheKey(int tenantId) => $"tenant:suspension:{tenantId}";

        public async Task<TenantSuspensionState> GetAsync(int tenantId, CancellationToken ct = default)
        {
            if (tenantId <= 0) return new TenantSuspensionState(false, null);

            if (_cache.TryGetValue<TenantSuspensionState>(CacheKey(tenantId), out var cached))
                return cached;

            var row = await _dbContext.Tenants
                .AsNoTracking()
                .Where(t => t.Id == tenantId)
                .Select(t => new { t.IsServiceSuspended, t.SuspensionMessage })
                .FirstOrDefaultAsync(ct);

            var state = row == null
                ? new TenantSuspensionState(false, null)
                : new TenantSuspensionState(row.IsServiceSuspended, row.SuspensionMessage);

            _cache.Set(CacheKey(tenantId), state, _ttl);
            return state;
        }
    }
}

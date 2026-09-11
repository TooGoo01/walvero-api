using Loyalty.Business.Dtos.Main;
using Loyalty.Business.Results;
using Loyalty.Business.Services.Abstractions.Main;
using Loyalty.DataAccess.Context;
using Loyalty.DataAccess.Entities.Main;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace Loyalty.Business.Services.Implementations.Main
{
    public class MenuPopupService : IMenuPopupService
    {
        private readonly LoyaltyWalletDbContext _db;
        private readonly IMemoryCache _cache;
        private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(60);
        private const string JoinBaseUrl = "https://walvero.com/join/";

        public MenuPopupService(LoyaltyWalletDbContext db, IMemoryCache cache)
        {
            _db = db;
            _cache = cache;
        }

        private static string PublicCacheKey(string slug) => $"menu-popup:{slug.ToLowerInvariant()}";

        public async Task<ServiceResult> ListAsync(int tenantId, CancellationToken ct = default)
        {
            var list = await _db.MenuPopups
                .AsNoTracking()
                .Where(p => p.TenantId == tenantId && p.IsActive)
                .OrderBy(p => p.SortOrder).ThenByDescending(p => p.Id)
                .Select(p => Map(p))
                .ToListAsync(ct);
            return new ServiceResult(true, list);
        }

        public async Task<ServiceResult> SaveAsync(int tenantId, SaveMenuPopupDto dto, CancellationToken ct = default)
        {
            MenuPopup e;
            if (dto.Id is > 0)
            {
                var found = await _db.MenuPopups
                    .FirstOrDefaultAsync(p => p.Id == dto.Id && p.TenantId == tenantId, ct);
                if (found == null) return new ServiceResult(false, "Popup tapılmadı");
                e = found;
                e.EditDate = DateTime.UtcNow;
            }
            else
            {
                e = new MenuPopup { TenantId = tenantId, RegDate = DateTime.UtcNow, IsActive = true };
                _db.MenuPopups.Add(e);
            }

            e.ImageUrl = dto.ImageUrl?.Trim();
            e.AzTitle = dto.AzTitle?.Trim();
            e.EnTitle = dto.EnTitle?.Trim();
            e.RuTitle = dto.RuTitle?.Trim();
            e.AzDescription = dto.AzDescription?.Trim();
            e.EnDescription = dto.EnDescription?.Trim();
            e.RuDescription = dto.RuDescription?.Trim();
            e.CtaEnabled = dto.CtaEnabled;
            e.CtaUrl = dto.CtaUrl?.Trim();
            e.AzCtaText = dto.AzCtaText?.Trim();
            e.EnCtaText = dto.EnCtaText?.Trim();
            e.RuCtaText = dto.RuCtaText?.Trim();
            e.IsEnabled = dto.IsEnabled;
            e.SortOrder = dto.SortOrder;

            await _db.SaveChangesAsync(ct);
            await InvalidatePublicCacheAsync(tenantId, ct);
            return new ServiceResult(true, Map(e));
        }

        public async Task<ServiceResult> DeleteAsync(int tenantId, int id, CancellationToken ct = default)
        {
            var e = await _db.MenuPopups.FirstOrDefaultAsync(p => p.Id == id && p.TenantId == tenantId, ct);
            if (e == null) return new ServiceResult(false, "Popup tapılmadı");
            _db.MenuPopups.Remove(e);
            await _db.SaveChangesAsync(ct);
            await InvalidatePublicCacheAsync(tenantId, ct);
            return new ServiceResult(true, "Silindi");
        }

        public async Task<ServiceResult> GetPublicAsync(string tenantSlug, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(tenantSlug))
                return new ServiceResult(true, (object?)null);

            var key = PublicCacheKey(tenantSlug);
            if (_cache.TryGetValue(key, out PublicMenuPopupDto? cached))
                return new ServiceResult(true, cached);

            var tenant = await _db.Tenants
                .AsNoTracking()
                .Where(t => t.Subdomain == tenantSlug && t.IsActive)
                .Select(t => new { t.Id, t.Subdomain })
                .FirstOrDefaultAsync(ct);

            PublicMenuPopupDto? dto = null;
            if (tenant != null)
            {
                var p = await _db.MenuPopups
                    .AsNoTracking()
                    .Where(x => x.TenantId == tenant.Id && x.IsActive && x.IsEnabled)
                    .OrderBy(x => x.SortOrder).ThenByDescending(x => x.Id)
                    .FirstOrDefaultAsync(ct);

                if (p != null)
                {
                    dto = new PublicMenuPopupDto
                    {
                        ImageUrl = p.ImageUrl,
                        AzTitle = p.AzTitle,
                        EnTitle = p.EnTitle,
                        RuTitle = p.RuTitle,
                        AzDescription = p.AzDescription,
                        EnDescription = p.EnDescription,
                        RuDescription = p.RuDescription,
                        CtaEnabled = p.CtaEnabled,
                        CtaUrl = string.IsNullOrWhiteSpace(p.CtaUrl)
                            ? JoinBaseUrl + tenant.Subdomain
                            : p.CtaUrl,
                        AzCtaText = p.AzCtaText,
                        EnCtaText = p.EnCtaText,
                        RuCtaText = p.RuCtaText,
                    };
                }
            }

            _cache.Set(key, dto, CacheTtl);
            return new ServiceResult(true, dto);
        }

        private async Task InvalidatePublicCacheAsync(int tenantId, CancellationToken ct)
        {
            var slug = await _db.Tenants
                .AsNoTracking()
                .Where(t => t.Id == tenantId)
                .Select(t => t.Subdomain)
                .FirstOrDefaultAsync(ct);
            if (!string.IsNullOrWhiteSpace(slug))
                _cache.Remove(PublicCacheKey(slug!));
        }

        private static GetMenuPopupDto Map(MenuPopup p) => new()
        {
            Id = p.Id,
            ImageUrl = p.ImageUrl,
            AzTitle = p.AzTitle,
            EnTitle = p.EnTitle,
            RuTitle = p.RuTitle,
            AzDescription = p.AzDescription,
            EnDescription = p.EnDescription,
            RuDescription = p.RuDescription,
            CtaEnabled = p.CtaEnabled,
            CtaUrl = p.CtaUrl,
            AzCtaText = p.AzCtaText,
            EnCtaText = p.EnCtaText,
            RuCtaText = p.RuCtaText,
            IsEnabled = p.IsEnabled,
            SortOrder = p.SortOrder,
        };
    }
}

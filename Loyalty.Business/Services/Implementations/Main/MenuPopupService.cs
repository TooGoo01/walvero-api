using Loyalty.Business.Dtos.Main;
using Loyalty.Business.Helpers;
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

        // Filial üzrə ayrıca cache; ləğv üçün tenant versiyası açarın içindədir (bütün filial açarları birdən köhnəlir)
        private static string VersionKey(string slug) => $"menu-popup-ver:{slug.ToLowerInvariant()}";
        private string PublicCacheKey(string slug, string? branchSlug)
            => $"menu-popup:{slug.ToLowerInvariant()}:{(branchSlug ?? "").Trim().ToLowerInvariant()}:{_cache.Get<int>(VersionKey(slug))}";

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

            // Filial hədəfi: yalnız tenant-ın öz filialları; boş = bütün filiallar
            var requested = dto.BranchIds?.Where(i => i > 0).Distinct().ToList() ?? new List<int>();
            if (requested.Count > 0)
            {
                var own = await _db.Branches.AsNoTracking()
                    .Where(b => b.TenantId == tenantId && requested.Contains(b.Id))
                    .Select(b => b.Id).ToListAsync(ct);
                if (own.Count != requested.Count) return new ServiceResult(false, "Seçilmiş filial tapılmadı");
            }
            e.BranchIdsJson = MenuPopupTargeting.Serialize(requested);

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

        public async Task<ServiceResult> GetPublicAsync(string tenantSlug, string? branchSlug = null, CancellationToken ct = default)
            => new ServiceResult(true, (await GetPublicListInternalAsync(tenantSlug, branchSlug, ct)).FirstOrDefault());

        public async Task<ServiceResult> GetPublicListAsync(string tenantSlug, string? branchSlug = null, CancellationToken ct = default)
            => new ServiceResult(true, await GetPublicListInternalAsync(tenantSlug, branchSlug, ct));

        /// <summary>Menyuda göstəriləcək bütün popup-lar (SortOrder sırası, filial filtri tətbiq olunub) — menu-app slayd kimi göstərir.</summary>
        private async Task<List<PublicMenuPopupDto>> GetPublicListInternalAsync(string tenantSlug, string? branchSlug, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(tenantSlug)) return new List<PublicMenuPopupDto>();

            var key = PublicCacheKey(tenantSlug, branchSlug);
            if (_cache.TryGetValue(key, out List<PublicMenuPopupDto>? cached) && cached != null)
                return cached;

            var tenant = await _db.Tenants
                .AsNoTracking()
                .Where(t => t.Subdomain == tenantSlug && t.IsActive)
                .Select(t => new { t.Id, t.Subdomain })
                .FirstOrDefaultAsync(ct);

            var list = new List<PublicMenuPopupDto>();
            if (tenant != null)
            {
                int? branchId = null;
                if (!string.IsNullOrWhiteSpace(branchSlug))
                    branchId = await _db.Branches.AsNoTracking()
                        .Where(b => b.TenantId == tenant.Id && b.Slug == branchSlug && b.IsActive)
                        .Select(b => (int?)b.Id)
                        .FirstOrDefaultAsync(ct);

                // Tenant-da popup sayı azdır — filial filtri yaddaşda (JSON sütunu)
                var candidates = await _db.MenuPopups
                    .AsNoTracking()
                    .Where(x => x.TenantId == tenant.Id && x.IsActive && x.IsEnabled)
                    .OrderBy(x => x.SortOrder).ThenByDescending(x => x.Id)
                    .ToListAsync(ct);

                list = candidates
                    .Where(p => MenuPopupTargeting.AppliesTo(p.BranchIdsJson, branchId))
                    .Select(p => new PublicMenuPopupDto
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
                    })
                    .ToList();
            }

            _cache.Set(key, list, CacheTtl);
            return list;
        }

        private async Task InvalidatePublicCacheAsync(int tenantId, CancellationToken ct)
        {
            var slug = await _db.Tenants
                .AsNoTracking()
                .Where(t => t.Id == tenantId)
                .Select(t => t.Subdomain)
                .FirstOrDefaultAsync(ct);
            if (!string.IsNullOrWhiteSpace(slug))
                _cache.Set(VersionKey(slug!), _cache.Get<int>(VersionKey(slug!)) + 1);
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
            BranchIds = MenuPopupTargeting.Parse(p.BranchIdsJson),
        };
    }
}

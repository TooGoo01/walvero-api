using Loyalty.Business.Dtos.Get.Main;
using Loyalty.Business.Results;

namespace Loyalty.Business.Services.Abstractions.Main
{
    public interface IPublicTenantDataService
    {
        /// <summary>
        /// GET /tenants — Bütün aktiv tenant-ları siyahıla (ad + təsvir)
        /// </summary>
        Task<ServiceResult<List<TenantListItemDto>>> GetAllTenantsAsync(CancellationToken ct = default);

        /// <summary>
        /// GET /tenants/{slug} — Tək tenant konfiqurasiyası (branding, wifi, menuApiUrl)
        /// </summary>
        Task<ServiceResult<TenantConfigDto>> GetTenantBySlugAsync(string slug, CancellationToken ct = default);

        /// <summary>
        /// GET /restaurants/{slug} — Restoran datası (sosial media, location, branding)
        /// </summary>
        Task<ServiceResult<RestaurantDataDto>> GetRestaurantBySlugAsync(string slug, CancellationToken ct = default);

        /// <summary>
        /// GET /restaurants/{tenantSlug}/{branchSlug} — Branch konteksti ilə restoran datası;
        /// links tenant + branch union qaytarır.
        /// </summary>
        Task<ServiceResult<RestaurantDataDto>> GetRestaurantBySlugAsync(string slug, string? branchSlug, CancellationToken ct = default);

        /// <summary>
        /// GET /menu/{slug} — Menyu datası kateqoriyaya görə qruplaşdırılmış (yalnız tenant-wide)
        /// </summary>
        Task<ServiceResult<List<MenuCategoryGroupDto>>> GetMenuBySlugAsync(string slug, CancellationToken ct = default);

        /// <summary>
        /// GET /menu/{tenantSlug}/{branchSlug} — Tenant-wide + branch-spesifik item union
        /// </summary>
        Task<ServiceResult<List<MenuCategoryGroupDto>>> GetMenuBySlugAsync(string tenantSlug, string? branchSlug, CancellationToken ct = default);
    }
}

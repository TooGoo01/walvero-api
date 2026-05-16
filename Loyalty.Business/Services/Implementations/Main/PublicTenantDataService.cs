using Loyalty.Business.Dtos.Get.Main;
using Loyalty.Business.Results;
using Loyalty.Business.Services.Abstractions.Main;
using Loyalty.DataAccess.Context;
using Microsoft.EntityFrameworkCore;

namespace Loyalty.Business.Services.Implementations.Main
{
    public class PublicTenantDataService : IPublicTenantDataService
    {
        private readonly LoyaltyWalletDbContext _dbContext;

        public PublicTenantDataService(LoyaltyWalletDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<ServiceResult<List<TenantListItemDto>>> GetAllTenantsAsync(CancellationToken ct = default)
        {
            var tenants = await _dbContext.Tenants
                .AsNoTracking()
                .Where(t => t.IsActive && !t.IsHidden && t.IsHasMenu)
                .OrderBy(t => t.CompanyName)
                .Select(t => new TenantListItemDto
                {
                    Name = t.CompanyName,
                    Slug = t.Subdomain,
                    Description = t.Branding != null ? t.Branding.Description : null
                })
                .ToListAsync(ct);

            return ServiceResult<List<TenantListItemDto>>.Ok(tenants);
        }

        public async Task<ServiceResult<TenantConfigDto>> GetTenantBySlugAsync(string slug, CancellationToken ct = default)
        {
            var tenant = await _dbContext.Tenants
                .AsNoTracking()
                .Include(t => t.Branding)
                .Include(t => t.RestorantWifiInformation)
                .Where(t => t.Subdomain == slug && t.IsActive && !t.IsHidden)
                .FirstOrDefaultAsync(ct);

            if (tenant == null)
                return ServiceResult<TenantConfigDto>.Fail("Tenant tapılmadı");

            var wifiList = tenant.RestorantWifiInformation?
                .Where(w => w.IsActive)
                .Select(w => new TenantConfigWifiDto
                {
                    Id = w.Id,
                    Password = w.Password,
                    SSID = w.SSID
                })
                .ToList() ?? new List<TenantConfigWifiDto>();

            var config = new TenantConfigDto
            {
                Id = tenant.Id,
                Name = tenant.CompanyName,
                Slug = tenant.Subdomain,
                Description = tenant.Branding?.Description,
                Logo = tenant.Branding?.LogoUrl,
                PrimaryColor = tenant.Branding?.PrimaryColor ?? "#000000",
                Domain = tenant.Subdomain,
                MenuApiUrl = tenant.IsHasMenu && !string.IsNullOrWhiteSpace(tenant.MenuUrl) ? tenant.MenuUrl : null,
                WifiInformation = wifiList,
                Branding = tenant.Branding != null
                    ? new TenantConfigBrandingDto
                    {
                        LogoUrl = tenant.Branding.LogoUrl,
                        BackgroundImageUrl = tenant.Branding.BackgroundImageUrl,
                        IconUrl = tenant.Branding.IconUrl,
                        PrimaryColor = tenant.Branding.PrimaryColor,
                        SecondaryColor = tenant.Branding.SecondaryColor,
                        BackgroundColor = tenant.Branding.BackgroundColor,
                        TextColor = tenant.Branding.TextColor,
                        Description = tenant.Branding.Description,
                        Website = tenant.Branding.Website
                    }
                    : null
            };

            return ServiceResult<TenantConfigDto>.Ok(config);
        }

        public async Task<ServiceResult<RestaurantDataDto>> GetRestaurantBySlugAsync(string slug, CancellationToken ct = default)
        {
            var tenant = await _dbContext.Tenants
                .AsNoTracking()
                .Include(t => t.Branding)
                .Include(t => t.TenantLocation)
                    .ThenInclude(tl => tl!.Country)
                .Include(t => t.TenantLocation)
                    .ThenInclude(tl => tl!.City)
                .Include(t => t.TenantLocation)
                    .ThenInclude(tl => tl!.TimeZone)
                .Include(t => t.Links!.Where(l => l.IsActive))
                .Where(t => t.Subdomain == slug && t.IsActive && !t.IsHidden)
                .AsSplitQuery()
                .FirstOrDefaultAsync(ct);

            if (tenant == null)
                return ServiceResult<RestaurantDataDto>.Fail("Restoran tapılmadı");

            var links = (tenant.Links ?? new List<DataAccess.Entities.Main.TenantLink>())
                .OrderBy(l => l.SortOrder)
                .ThenBy(l => l.Id)
                .Select(l => new RestaurantLinkDto
                {
                    Id = l.Id,
                    AzTitle = l.AzTitle,
                    EnTitle = l.EnTitle,
                    RuTitle = l.RuTitle,
                    Url = l.Url,
                    IconKey = l.IconKey,
                    SortOrder = l.SortOrder,
                    OpenInNewTab = l.OpenInNewTab
                })
                .ToList();

            var dto = new RestaurantDataDto
            {
                Id = tenant.Id,
                Name = tenant.CompanyName,
                Slug = tenant.Subdomain,
                ContactEmail = tenant.ContactEmail,
                ContactPhone = tenant.ContactPhone,
                LocationUrl = tenant.LocationUrl,
                WazeLocationUrl = tenant.WazeLocationUrl,
                InstagramUrl = tenant.InstagramUrl,
                FacebookUrl = tenant.FacebookUrl,
                WhatsAppUrl = tenant.WhatsAppUrl,
                TelegramUrl = tenant.TeleqramUrl,
                LinkedInUrl = tenant.LinkedInUrl,
                TiktokUrl = tenant.TiktokUrl,
                TripAdvisorUrl = tenant.TripAdvisorUrl,
                WebsiteUrl = tenant.WebsiteUrl,
                YoutubeUrl = tenant.YoutubeUrl,
                TwitterUrl = tenant.TwitterUrl,
                YelpUrl = tenant.YelpUrl,
                ThreadsUrl = tenant.ThreadsUrl,
                PinterestUrl = tenant.PinterestUrl,
                Branding = tenant.Branding != null
                    ? new TenantConfigBrandingDto
                    {
                        LogoUrl = tenant.Branding.LogoUrl,
                        BackgroundImageUrl = tenant.Branding.BackgroundImageUrl,
                        IconUrl = tenant.Branding.IconUrl,
                        PrimaryColor = tenant.Branding.PrimaryColor,
                        SecondaryColor = tenant.Branding.SecondaryColor,
                        BackgroundColor = tenant.Branding.BackgroundColor,
                        TextColor = tenant.Branding.TextColor,
                        Description = tenant.Branding.Description,
                        Website = tenant.Branding.Website
                    }
                    : null,
                Location = tenant.TenantLocation != null
                    ? new RestaurantLocationDto
                    {
                        CountryName = tenant.TenantLocation.Country?.AzName,
                        CityName = tenant.TenantLocation.City?.AzName,
                        TimeZoneName = tenant.TenantLocation.TimeZone?.Name
                    }
                    : null,
                Links = links
            };

            return ServiceResult<RestaurantDataDto>.Ok(dto);
        }

        public async Task<ServiceResult<List<MenuCategoryGroupDto>>> GetMenuBySlugAsync(string slug, CancellationToken ct = default)
        {
            var tenant = await _dbContext.Tenants
                .AsNoTracking()
                .Where(t => t.Subdomain == slug && t.IsActive && !t.IsHidden)
                .Select(t => new { t.Id })
                .FirstOrDefaultAsync(ct);

            if (tenant == null)
                return ServiceResult<List<MenuCategoryGroupDto>>.Fail("Tenant tapılmadı");

            var categories = await _dbContext.MenuCategories
                .AsNoTracking()
                .Where(mc => mc.TenantId == tenant.Id && mc.IsActive)
                .Include(mc => mc.MenuItems.Where(mi => mi.IsActive))
                    .ThenInclude(mi => mi.MenuItemImages)
                .OrderBy(mc => mc.Id)
                .ToListAsync(ct);

            var result = categories.Select(mc => new MenuCategoryGroupDto
            {
                Id = mc.Id,
                AzName = mc.AzName,
                EnName = mc.EnName,
                RuName = mc.RuName,
                AzDescription = mc.AzDescription,
                EnDescription = mc.EnDescription,
                RuDescription = mc.RuDescription,
                Items = mc.MenuItems.Select(mi => new MenuItemSummaryDto
                {
                    Id = mi.Id,
                    AzName = mi.AzName,
                    EnName = mi.EnName,
                    RuName = mi.RuName,
                    AzDescription = mi.AzDescription,
                    EnDescription = mi.EnDescription,
                    RuDescription = mi.RuDescription,
                    Currency = mi.Currency,
                    CurrencySign = mi.CurrencySign,
                    Price = mi.Price,
                    DiscountPrice = mi.DiscountPrice,
                    PrepTimeMinutes = mi.PrepTimeMinutes,
                    ImageUrls = mi.MenuItemImages.Select(img => img.Path).ToList()
                }).ToList()
            }).ToList();

            return ServiceResult<List<MenuCategoryGroupDto>>.Ok(result);
        }
    }
}

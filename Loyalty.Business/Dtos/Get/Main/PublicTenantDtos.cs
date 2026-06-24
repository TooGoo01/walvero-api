namespace Loyalty.Business.Dtos.Get.Main
{
    /// <summary>
    /// GET /tenants — Sadəcə adları və təsvirləri (sitemap + admin panel üçün)
    /// </summary>
    public class TenantListItemDto
    {
        public string Name { get; set; } = string.Empty;
        public string Slug { get; set; } = string.Empty;
        public string? Description { get; set; }
    }

    /// <summary>
    /// GET /tenants/{slug} — Tam tenant konfiqurasiyası (layout tərəfindən çağırılır)
    /// </summary>
    public class TenantConfigDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Slug { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? Logo { get; set; }
        public string PrimaryColor { get; set; } = "#000000";
        public string? Domain { get; set; }
        public string? MenuApiUrl { get; set; }
        public List<TenantConfigWifiDto> WifiInformation { get; set; } = new();
        public TenantConfigBrandingDto? Branding { get; set; }

        /// <summary>
        /// Slug-lı (URL-lı) branchlar. Slug yoxdursa exclude — backward compat üçün.
        /// </summary>
        public List<TenantConfigBranchDto> Branches { get; set; } = new();
    }

    public class TenantConfigBranchDto
    {
        public int Id { get; set; }
        public string Slug { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? Address { get; set; }
        public bool IsMainBranch { get; set; }

        public string? Phone { get; set; }
        public string? Email { get; set; }
        public string? PhotoUrl { get; set; }
        public string? LogoUrl { get; set; }
        public string? LocationUrl { get; set; }
        public string? WazeLocationUrl { get; set; }
        public string? MenuUrl { get; set; }
        public string? InstagramUrl { get; set; }
        public string? FacebookUrl { get; set; }
        public string? WhatsAppUrl { get; set; }
        public string? TelegramUrl { get; set; }
        public string? LinkedInUrl { get; set; }
        public string? TiktokUrl { get; set; }
        public string? TripAdvisorUrl { get; set; }
        public string? WebsiteUrl { get; set; }
        public string? YoutubeUrl { get; set; }
        public string? TwitterUrl { get; set; }
        public string? YelpUrl { get; set; }
        public string? ThreadsUrl { get; set; }
        public string? PinterestUrl { get; set; }
    }

    public class TenantConfigWifiDto
    {
        public int Id { get; set; }
        public string Password { get; set; } = string.Empty;
        public string SSID { get; set; } = string.Empty;
    }

    public class TenantConfigBrandingDto
    {
        public string? LogoUrl { get; set; }
        public string? BackgroundImageUrl { get; set; }
        public string? IconUrl { get; set; }
        public string PrimaryColor { get; set; } = "#000000";
        public string SecondaryColor { get; set; } = "#FFFFFF";
        public string BackgroundColor { get; set; } = "#FFFFFF";
        public string TextColor { get; set; } = "#000000";
        public string? Description { get; set; }
        public string? Website { get; set; }
    }

    /// <summary>
    /// GET /restaurants/{slug} — Restoran datası (sosial media, location, branding)
    /// </summary>
    public class RestaurantDataDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Slug { get; set; } = string.Empty;
        public string? ContactEmail { get; set; }
        public string? ContactPhone { get; set; }

        // Social media
        public string? LocationUrl { get; set; }
        public string? WazeLocationUrl { get; set; }
        public string? InstagramUrl { get; set; }
        public string? FacebookUrl { get; set; }
        public string? WhatsAppUrl { get; set; }
        public string? TelegramUrl { get; set; }
        public string? LinkedInUrl { get; set; }
        public string? TiktokUrl { get; set; }
        public string? TripAdvisorUrl { get; set; }
        public string? WebsiteUrl { get; set; }
        public string? YoutubeUrl { get; set; }
        public string? TwitterUrl { get; set; }
        public string? YelpUrl { get; set; }
        public string? ThreadsUrl { get; set; }
        public string? PinterestUrl { get; set; }

        // Branding
        public TenantConfigBrandingDto? Branding { get; set; }

        // Location
        public RestaurantLocationDto? Location { get; set; }

        // Dinamik linklər (sıralı)
        public List<RestaurantLinkDto> Links { get; set; } = new();

        /// <summary>
        /// Branch slug verilmişdirsə bu seçilmiş branchın overrideləri.
        /// Frontend logo/phone/sosial UI üçün branch dəyərlərini üstün tut.
        /// </summary>
        public RestaurantBranchDto? Branch { get; set; }

        /// <summary>
        /// Branch context-də tenant-wide + branch-spesifik wifi union.
        /// </summary>
        public List<TenantConfigWifiDto> WifiInformation { get; set; } = new();
    }

    public class RestaurantBranchDto
    {
        public int Id { get; set; }
        public string Slug { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? Address { get; set; }
        public string? Phone { get; set; }
        public string? Email { get; set; }
        public string? LogoUrl { get; set; }
        public string? PhotoUrl { get; set; }
        public string? InstagramUrl { get; set; }
        public string? FacebookUrl { get; set; }
        public string? WhatsAppUrl { get; set; }
        public string? TelegramUrl { get; set; }
        public string? LinkedInUrl { get; set; }
        public string? TiktokUrl { get; set; }
        public string? YoutubeUrl { get; set; }
        public string? TwitterUrl { get; set; }
        public string? TripAdvisorUrl { get; set; }
        public string? YelpUrl { get; set; }
        public string? ThreadsUrl { get; set; }
        public string? PinterestUrl { get; set; }
        public string? WebsiteUrl { get; set; }
        public string? LocationUrl { get; set; }
        public string? WazeLocationUrl { get; set; }
        public string? MenuUrl { get; set; }
        public string? BackgroundColor { get; set; }
        public string? ForegroundColor { get; set; }

        // Banner — video varsa frontend onu üstün tutur, yoxsa şəkillərdən slideshow göstərir.
        public string? BannerVideoUrl { get; set; }
        public string? BannerVideoPosterUrl { get; set; }
        public List<string> BannerImages { get; set; } = new();

        // Menu page-də 3 dil elan zolağı.
        public string? AnnouncementAz { get; set; }
        public string? AnnouncementEn { get; set; }
        public string? AnnouncementRu { get; set; }
    }

    public class RestaurantLocationDto
    {
        public string? CountryName { get; set; }
        public string? CityName { get; set; }
        public string? TimeZoneName { get; set; }
    }

    public class RestaurantLinkDto
    {
        public int Id { get; set; }
        public string AzTitle { get; set; } = string.Empty;
        public string? EnTitle { get; set; }
        public string? RuTitle { get; set; }
        public string Url { get; set; } = string.Empty;
        public string? IconKey { get; set; }
        public int SortOrder { get; set; }
        public bool OpenInNewTab { get; set; }
        public List<LinkActionDto>? Actions { get; set; }
    }

    /// <summary>
    /// GET /menu/{slug} — Menyu datası kateqoriyaya görə qruplaşdırılmış
    /// </summary>
    public class MenuCategoryGroupDto
    {
        public int Id { get; set; }
        public string AzName { get; set; } = string.Empty;
        public string EnName { get; set; } = string.Empty;
        public string RuName { get; set; } = string.Empty;
        public string? AzDescription { get; set; }
        public string? EnDescription { get; set; }
        public string? RuDescription { get; set; }
        public List<MenuItemSummaryDto> Items { get; set; } = new();
    }

    public class MenuItemSummaryDto
    {
        public int Id { get; set; }
        public string AzName { get; set; } = string.Empty;
        public string EnName { get; set; } = string.Empty;
        public string RuName { get; set; } = string.Empty;
        public string? AzDescription { get; set; }
        public string? EnDescription { get; set; }
        public string? RuDescription { get; set; }
        public string Currency { get; set; } = string.Empty;
        public string CurrencySign { get; set; } = string.Empty;
        public double Price { get; set; }
        public double DiscountPrice { get; set; }
        public string? PrepTimeMinutes { get; set; }
        public List<string> ImageUrls { get; set; } = new();
        public string? IngredientVideoUrl { get; set; }
        public string? IngredientVideoPosterUrl { get; set; }
    }
}

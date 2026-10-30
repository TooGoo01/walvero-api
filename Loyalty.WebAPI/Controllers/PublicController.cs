using Loyalty.Business.Dtos.Get.Main;
using Loyalty.Business.Results;
using Loyalty.Business.Services.Abstractions.Main;
using Microsoft.AspNetCore.Mvc;
using System.Net;

namespace Loyalty.WebAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class PublicController : ControllerBase
    {
        private readonly IHomeService _homeService;
        private readonly ICountrySequenceService _countrySequenceService;
        private readonly IPublicTenantDataService _publicTenantDataService;
        private readonly IAppSettingService _appSettingService;

        public PublicController(
            IHomeService homeService,
            ICountrySequenceService countrySequenceService,
            IPublicTenantDataService publicTenantDataService,
            IAppSettingService appSettingService)
        {
            _homeService = homeService;
            _countrySequenceService = countrySequenceService;
            _publicTenantDataService = publicTenantDataService;
            _appSettingService = appSettingService;
        }

        /// <summary>
        /// Mobil app-ın startup-da oxuduğu qlobal konfiqurasiya
        /// </summary>
        [HttpGet("app-config")]
        [ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> GetAppConfig(CancellationToken ct)
        {
            var showMapInApp = await _appSettingService.GetBoolAsync("ShowMapInApp", defaultValue: true, ct);
            var appLogoUrl = await _appSettingService.GetStringAsync("AppLogoUrl", "", ct);
            // Splash dizaynı — admin paneldən (AppSettings key-value). Boş = default.
            var splashBackgroundImageUrl = await _appSettingService.GetStringAsync("SplashBackgroundImageUrl", "", ct);
            var splashBgColor = await _appSettingService.GetStringAsync("SplashBgColor", "", ct);
            var splashTagline = await _appSettingService.GetStringAsync("SplashTagline", "", ct);
            // Sloqan stili — boş = app-dakı default (15px, w500, avtomatik kontrast rəngi)
            var splashTaglineColor = await _appSettingService.GetStringAsync("SplashTaglineColor", "", ct);
            var splashTaglineFontSize = await _appSettingService.GetStringAsync("SplashTaglineFontSize", "", ct);
            var splashTaglineBold = await _appSettingService.GetStringAsync("SplashTaglineBold", "", ct);
            // iOS deferred deep-link clipboard dialoqunun mətnləri (boş = app l10n defaultu)
            var clipboardPromptTitleAz = await _appSettingService.GetStringAsync("ClipboardPromptTitleAz", "", ct);
            var clipboardPromptTitleEn = await _appSettingService.GetStringAsync("ClipboardPromptTitleEn", "", ct);
            var clipboardPromptTitleRu = await _appSettingService.GetStringAsync("ClipboardPromptTitleRu", "", ct);
            var clipboardPromptBodyAz = await _appSettingService.GetStringAsync("ClipboardPromptBodyAz", "", ct);
            var clipboardPromptBodyEn = await _appSettingService.GetStringAsync("ClipboardPromptBodyEn", "", ct);
            var clipboardPromptBodyRu = await _appSettingService.GetStringAsync("ClipboardPromptBodyRu", "", ct);
            // AI köməkçi düyməsi: görünmə + görünüş (boş = app defaultları)
            var aiAssistantEnabled = await _appSettingService.GetBoolAsync("AiAssistantEnabled", defaultValue: true, ct);
            var aiButtonColor1 = await _appSettingService.GetStringAsync("AiButtonColor1", "", ct);
            var aiButtonColor2 = await _appSettingService.GetStringAsync("AiButtonColor2", "", ct);
            var aiButtonSize = await _appSettingService.GetStringAsync("AiButtonSize", "", ct);
            return Ok(new ServiceResult(true, new
            {
                showMapInApp,
                appLogoUrl,
                splashBackgroundImageUrl,
                splashBgColor,
                splashTagline,
                splashTaglineColor,
                splashTaglineFontSize,
                splashTaglineBold,
                clipboardPromptTitleAz,
                clipboardPromptTitleEn,
                clipboardPromptTitleRu,
                clipboardPromptBodyAz,
                clipboardPromptBodyEn,
                clipboardPromptBodyRu,
                aiAssistantEnabled,
                aiButtonColor1,
                aiButtonColor2,
                aiButtonSize
            }));
        }

        /// <summary>
        /// Get home page tenants (IsActive and ShowOnHome) with optional location filtering
        /// </summary>
        [HttpGet("home/tenants")]
        [ProducesResponseType(typeof(ServiceResult<List<HomeTenantDto>>), (int)HttpStatusCode.OK)]
        [ResponseCache(Duration = 120, VaryByQueryKeys = new[] { "countryId", "cityId", "platform" })]
        public async Task<ActionResult<ServiceResult<List<HomeTenantDto>>>> GetHomeTenants(
            [FromQuery] int? countryId,
            [FromQuery] int? cityId,
            [FromQuery] string? platform,
            CancellationToken ct)
        {
            // "web" platformu HideInApp tenant-ları da görür; mobil app (param yoxdur) görmür.
            var excludeHiddenInApp = !string.Equals(platform, "web", StringComparison.OrdinalIgnoreCase);
            var result = await _homeService.GetHomeTenantsAsync(countryId, cityId, excludeHiddenInApp, ct);
            
            if (result.Success)
            {
                return Ok(result);
            }

            return BadRequest(result);
        }

        /// <summary>
        /// Get home page stories (active, ordered by newest)
        /// </summary>
        [HttpGet("home/stories")]
        [ProducesResponseType(typeof(ServiceResult<List<HomeStoryDto>>), (int)HttpStatusCode.OK)]
        public async Task<ActionResult<ServiceResult<List<HomeStoryDto>>>> GetHomeStories(CancellationToken ct)
        {
            var result = await _homeService.GetHomeStoriesAsync(ct);
            
            if (result.Success)
            {
                return Ok(result);
            }

            return BadRequest(result);
        }

        /// <summary>
        /// Menu-app kampaniya popup-u — tenant-ın birinci aktiv popup-u (yoxdursa data=null).
        /// ?branch={branchSlug}: filial menyusu — həmin filiala və ya bütün filiallara aid popup; yoxdursa yalnız bütün filiallara aid.
        /// </summary>
        [HttpGet("menu-popup/{tenantSlug}")]
        [ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> GetMenuPopup(string tenantSlug, [FromQuery] string? branch, [FromServices] IMenuPopupService menuPopupService, CancellationToken ct)
        {
            return Ok(await menuPopupService.GetPublicAsync(tenantSlug, branch, ct));
        }

        /// <summary>
        /// Menu-app kampaniya popup-ları — bütün aktiv popup-lar (SortOrder sırası), menyu slayd kimi göstərir. data = [] yoxdursa.
        /// ?branch={branchSlug}: həmin filiala və ya bütün filiallara aid olanlar; yoxdursa yalnız bütün filiallara aid.
        /// </summary>
        [HttpGet("menu-popups/{tenantSlug}")]
        [ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> GetMenuPopups(string tenantSlug, [FromQuery] string? branch, [FromServices] IMenuPopupService menuPopupService, CancellationToken ct)
        {
            return Ok(await menuPopupService.GetPublicListAsync(tenantSlug, branch, ct));
        }

        /// <summary>Link seçimi səhifəsi (/l/{slug}) — dərc olunmuş, yalnız aktiv elementlər.</summary>
        [HttpGet("link-pages/{slug}")]
        [ResponseCache(Duration = 30, VaryByQueryKeys = new[] { "slug" })]
        [ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> GetLinkPage(string slug, [FromServices] ILinkPageService linkPages, CancellationToken ct)
        {
            var r = await linkPages.GetPublicBySlugAsync(slug, ct);
            return r.Success ? Ok(r) : NotFound(r);
        }

        /// <summary>Link seçimi səhifəsində klik (anonim, fire-and-forget — həmişə 200).</summary>
        [HttpPost("link-pages/{slug}/click/{itemId:int}")]
        public async Task<IActionResult> TrackLinkPageClick(string slug, int itemId, [FromServices] ILinkPageService linkPages, CancellationToken ct)
        {
            await linkPages.TrackClickAsync(slug, itemId, ct);
            return Ok(new ServiceResult(true));
        }

        /// <summary>
        /// Get home page offers (active, ordered by newest)
        /// </summary>
        [HttpGet("home/offers")]
        [ProducesResponseType(typeof(ServiceResult<List<HomeOfferDto>>), (int)HttpStatusCode.OK)]
        public async Task<ActionResult<ServiceResult<List<HomeOfferDto>>>> GetHomeOffers(CancellationToken ct)
        {
            var result = await _homeService.GetHomeOffersAsync(ct);
            
            if (result.Success)
            {
                return Ok(result);
            }

            return BadRequest(result);
        }

        /// <summary>
        /// Get active country sequences
        /// </summary>
        [HttpGet("countries")]
        [ProducesResponseType(typeof(ServiceResult<List<GetCountrySequenceDto>>), (int)HttpStatusCode.OK)]
        public async Task<ActionResult<ServiceResult<List<GetCountrySequenceDto>>>> GetActiveCountries(CancellationToken ct)
        {
            var result = await _countrySequenceService.GetActiveCountriesAsync(ct);
            
            if (result.Success)
            {
                return Ok(result);
            }

            return BadRequest(result);
        }

        // ========== PUBLIC TENANT DATA ENDPOINTS ==========

        /// <summary>
        /// Bütün aktiv tenant-ları siyahıla (ad + təsvir). Sitemap və admin panel üçün.
        /// </summary>
        [HttpGet("tenants")]
        [ProducesResponseType(typeof(ServiceResult<List<TenantListItemDto>>), (int)HttpStatusCode.OK)]
        [ResponseCache(Duration = 120)]
        public async Task<ActionResult<ServiceResult<List<TenantListItemDto>>>> GetTenants(CancellationToken ct)
        {
            var result = await _publicTenantDataService.GetAllTenantsAsync(ct);
            return Ok(result);
        }

        /// <summary>
        /// Tək tenant konfiqurasiyası slug ilə. Layout tərəfindən çağırılır.
        /// 200 → tenant config, 404 → tenant yoxdur
        /// </summary>
        [HttpGet("tenants/{slug}")]
        [ProducesResponseType(typeof(ServiceResult<TenantConfigDto>), (int)HttpStatusCode.OK)]
        [ProducesResponseType(typeof(ServiceResult<TenantConfigDto>), (int)HttpStatusCode.NotFound)]
        [ResponseCache(Duration = 60, VaryByQueryKeys = new[] { "slug" })]
        public async Task<ActionResult<ServiceResult<TenantConfigDto>>> GetTenantBySlug(string slug, CancellationToken ct)
        {
            var result = await _publicTenantDataService.GetTenantBySlugAsync(slug, ct);
            if (!result.Success)
                return NotFound(result);

            return Ok(result);
        }

        /// <summary>
        /// Restoran datası: sosial media, location, branding.
        /// URL tenant config-dəki menuApiUrl-dən gəlir.
        /// </summary>
        [HttpGet("restaurants/{slug}")]
        [ProducesResponseType(typeof(ServiceResult<RestaurantDataDto>), (int)HttpStatusCode.OK)]
        [ProducesResponseType(typeof(ServiceResult<RestaurantDataDto>), (int)HttpStatusCode.NotFound)]
        [ResponseCache(Duration = 60)]
        public async Task<ActionResult<ServiceResult<RestaurantDataDto>>> GetRestaurantBySlug(string slug, CancellationToken ct)
        {
            var result = await _publicTenantDataService.GetRestaurantBySlugAsync(slug, ct);
            if (!result.Success)
                return NotFound(result);

            return Ok(result);
        }

        /// <summary>
        /// Branch konteksti ilə restoran datası: branch override (logo, sosial) + tenant link + branch wifi union.
        /// </summary>
        [HttpGet("restaurants/{tenantSlug}/{branchSlug}")]
        [ProducesResponseType(typeof(ServiceResult<RestaurantDataDto>), (int)HttpStatusCode.OK)]
        [ProducesResponseType(typeof(ServiceResult<RestaurantDataDto>), (int)HttpStatusCode.NotFound)]
        [ResponseCache(Duration = 60, VaryByQueryKeys = new[] { "tenantSlug", "branchSlug" })]
        public async Task<ActionResult<ServiceResult<RestaurantDataDto>>> GetRestaurantByTenantAndBranch(string tenantSlug, string branchSlug, CancellationToken ct)
        {
            var result = await _publicTenantDataService.GetRestaurantBySlugAsync(tenantSlug, branchSlug, ct);
            if (!result.Success)
                return NotFound(result);
            return Ok(result);
        }

        /// <summary>
        /// Menyu datası kateqoriyaya görə qruplaşdırılmış (yalnız tenant-wide).
        /// </summary>
        [HttpGet("menu/{slug}")]
        [ProducesResponseType(typeof(ServiceResult<List<MenuCategoryGroupDto>>), (int)HttpStatusCode.OK)]
        [ProducesResponseType(typeof(ServiceResult<List<MenuCategoryGroupDto>>), (int)HttpStatusCode.NotFound)]
        [ResponseCache(Duration = 60)]
        public async Task<ActionResult<ServiceResult<List<MenuCategoryGroupDto>>>> GetMenuBySlug(string slug, CancellationToken ct)
        {
            var result = await _publicTenantDataService.GetMenuBySlugAsync(slug, ct);
            if (!result.Success)
                return NotFound(result);

            return Ok(result);
        }

        /// <summary>
        /// Branch-spesifik menyu: tenant-wide + branch item union.
        /// </summary>
        [HttpGet("menu/{tenantSlug}/{branchSlug}")]
        [ProducesResponseType(typeof(ServiceResult<List<MenuCategoryGroupDto>>), (int)HttpStatusCode.OK)]
        [ProducesResponseType(typeof(ServiceResult<List<MenuCategoryGroupDto>>), (int)HttpStatusCode.NotFound)]
        [ResponseCache(Duration = 60, VaryByQueryKeys = new[] { "tenantSlug", "branchSlug" })]
        public async Task<ActionResult<ServiceResult<List<MenuCategoryGroupDto>>>> GetMenuByTenantAndBranch(string tenantSlug, string branchSlug, CancellationToken ct)
        {
            var result = await _publicTenantDataService.GetMenuBySlugAsync(tenantSlug, branchSlug, ct);
            if (!result.Success)
                return NotFound(result);

            return Ok(result);
        }
    }
}


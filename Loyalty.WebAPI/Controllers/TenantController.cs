using Loyalty.Business.Dtos.Get.Main;
using Loyalty.Business.Dtos.Post.Main;
using Loyalty.Business.Results;
using Loyalty.Business.Services.Abstractions.Main;
using Loyalty.DataAccess.Context;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Net;
using System.Security.Claims;

namespace Loyalty.WebAPI.Controllers
{
    [Route("api/[controller]/[action]")]
    [ApiController]
    public class TenantController : ControllerBase
    {
        private readonly ITenantService _tenantService;
        private readonly ICountryService _countryService;
        private readonly ICityService _cityService;
        private readonly ITimeZoneService _timeZoneService;
        private readonly ITenantProvider _tenantProvider;
        private readonly LoyaltyWalletDbContext _db;
        public TenantController(ITenantService tenantService, ITimeZoneService timeZoneService, ICityService cityService, ICountryService countryService, ITenantProvider tenantProvider, LoyaltyWalletDbContext db)
        {
            _tenantService = tenantService;
            _timeZoneService = timeZoneService;
            _cityService = cityService;
            _countryService = countryService;
            _tenantProvider = tenantProvider;
            _db = db;
        }

        /// <summary>
        /// Super Admin: discover səhifəsi tenant sıralamasını yenilə.
        /// </summary>
        [HttpPost]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Roles = "Super Admin,SuperAdmin")]
        [ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> ReorderDiscover([FromBody] ReorderTenantsDto dto)
        {
            if (dto?.Items == null || dto.Items.Count == 0)
                return BadRequest(new ServiceResult(false, "Items boşdur"));

            var ids = dto.Items.Select(i => i.Id).ToList();
            var tenants = await _db.Tenants.Where(t => ids.Contains(t.Id)).ToListAsync();
            foreach (var item in dto.Items)
            {
                var t = tenants.FirstOrDefault(x => x.Id == item.Id);
                if (t != null) t.DiscoverSortOrder = item.SortOrder;
            }
            await _db.SaveChangesAsync();
            return Ok(new ServiceResult(true, "Sıra yeniləndi"));
        }

        /// <summary>Returns the current authenticated tenant's info (Tenant Admin / Moderator).</summary>
        [HttpGet]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Policy = "TenantAdminOrModerator")]
        [ProducesResponseType(typeof(ServiceResult<GetTenantDto>), (int)HttpStatusCode.OK)]
        [ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.Unauthorized)]
        public async Task<IActionResult> GetMy()
        {
            var tenantId = _tenantProvider.CurrentTenantId;
            if (!tenantId.HasValue) return Unauthorized(new ServiceResult(false, "Tenant not found in token"));
            var result = await _tenantService.GetById(tenantId.Value);
            return Ok(result);
        }

        /// <summary>
        /// Get active tenants with optional filtering by category, location, and name.
        /// Supports both authenticated (JWT) and anonymous requests.
        /// Authenticated users get IsCustomer flag based on their loyalty cards.
        /// Anonymous users get IsCustomer = false for all tenants.
        /// Always returns 200 OK (never 401).
        /// </summary>
        [AllowAnonymous]
        [HttpGet]
        [ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.OK)]
        [ResponseCache(
            Duration = 60,
            VaryByHeader = "Authorization",
            VaryByQueryKeys = new[] { "currentPage", "pageSize", "categoryId", "countryId", "cityId", "name", "platform" })]
        public async Task<ActionResult<ServiceResult>> GetTenants(
            [FromQuery] TenantSearchDto? model,
            int currentPage = 1,
            int pageSize = 10)
        {
            // [AllowAnonymous] causes ASP.NET Core 8 to skip automatic JWT authentication.
            // Manually authenticate to read the token if present.
            int? currentUserId = null;
            var authResult = await HttpContext.AuthenticateAsync(JwtBearerDefaults.AuthenticationScheme);
            if (authResult.Succeeded && authResult.Principal != null)
            {
                var userIdStr = authResult.Principal.FindFirst(ClaimTypes.NameIdentifier)?.Value
                             ?? authResult.Principal.FindFirst("Id")?.Value;
                if (int.TryParse(userIdStr, out var uid))
                    currentUserId = uid;
            }

            return Ok(await _tenantService.GetActiveUser(currentPage, pageSize, model, currentUserId));
        }

        [HttpGet]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Policy = "SuperAdminOnly")]
        [ProducesResponseType(typeof(ServiceResult<List<GetTenantDto>>), (int)HttpStatusCode.OK)]
        [ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.NotFound)]
        [ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.Unauthorized)]
        [ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.BadRequest)]
        public async Task<IActionResult> List()
        {
            var result = await _tenantService.GetAll();
            return Ok(result);
        }

        /// <summary>
        /// Tenant-ı aktiv/deaktiv edir. Deaktiv tenant-da bütün əməliyyatlar
        /// (skan, earn/redeem, POS webhook, yeni qoşulma) dayanır və tenant
        /// discover/home siyahılarından itir. Yalnız Super Admin.
        /// </summary>
        [HttpPost]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Policy = "SuperAdminOnly")]
        [ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.OK)]
        [ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.NotFound)]
        public async Task<IActionResult> SetActive([FromQuery] int id, [FromQuery] bool active)
        {
            var result = await _tenantService.SetActiveAsync(id, active);
            if (!result.Success)
                return NotFound(result);
            return Ok(result);
        }

        /// <summary>
        /// Ödəniş suspensiyası: müştəri kartlarında ad-soyad yerinə mesaj, balans/mükafat gizli.
        /// Display-only mask — data dəyişmir, earn/redeem işləyir; restore = suspended=false.
        /// Toggle zamanı tenant-ın bütün Google/Apple Wallet pass-larına batch yeniləmə push olunur.
        /// message boş string göndərilsə custom mesaj silinir (default-a qayıdır); null → toxunulmur.
        /// </summary>
        [HttpPost]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Policy = "SuperAdminOnly")]
        [ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.OK)]
        [ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.NotFound)]
        public async Task<IActionResult> SetServiceSuspended(
            [FromQuery] int id, [FromQuery] bool suspended, [FromQuery] string? message = null)
        {
            var result = await _tenantService.SetServiceSuspendedAsync(id, suspended, message);
            if (!result.Success)
                return NotFound(result);
            return Ok(result);
        }

        [HttpPost]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Policy = "SuperAdminOnly")]
        [ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.OK)]
        [ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.NotFound)]
        [ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.Unauthorized)]
        [ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.BadRequest)]
        public async Task<IActionResult> Add(AddTenantDto addTenantDto)
        {
            if (!ModelState.IsValid)
            {
                foreach (var error in ModelState.Values.SelectMany(modelState => modelState.Errors))
                {
                    ModelState.AddModelError("", error.ErrorMessage);
                }
                return BadRequest(ModelState);
            }
            var result = await _tenantService.Add(addTenantDto);
            if (result.Success)
                return Ok(result);

            return BadRequest(result);
        }

        [HttpPost]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Policy = "SuperAdminOnly")]
        [ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.OK)]
        [ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.NotFound)]
        [ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.Unauthorized)]
        [ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.BadRequest)]
        public async Task<IActionResult> Update(UpdateTenantDto updateTenantDto)
        {
            if (!ModelState.IsValid)
            {
                var tenant = await _tenantService.GetById(updateTenantDto.Id);
                var tenantDto = tenant.Data as GetTenantDto;
                if (tenantDto == null)
                {
                    return NotFound();
                }

                foreach (var error in ModelState.Values.SelectMany(modelState => modelState.Errors))
                {
                    ModelState.AddModelError("", error.ErrorMessage);
                }
                return BadRequest(ModelState);
            }
            var result = await _tenantService.Update(updateTenantDto);
            if (result.Success)
                return Ok(result);

            return BadRequest(result);
        }
    }
}

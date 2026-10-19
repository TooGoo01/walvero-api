using Loyalty.Business.Dtos.Get.Main;
using Loyalty.Business.Dtos.Post.Main;
using Loyalty.Business.Results;
using Loyalty.Business.Services.Abstractions.Main;
using Loyalty.WebAPI.Tenancy;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Net;

namespace Loyalty.WebAPI.Controllers
{
    [Route("api/[controller]/[action]")]
    [ApiController]
    [Authorize(
    AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme,
    Policy = "TenantAdminOrModerator")]
    public class LoyaltyProgramController : ControllerBase
    {
        private readonly ILoyaltyProgramService _loyaltyProgramService;
        private readonly ITenantProvider _tenantProvider;
        private readonly IAmbassadorProgramFactory _ambassadorFactory;

        public LoyaltyProgramController(ITenantProvider tenantProvider, ILoyaltyProgramService loyaltyProgramService,
            IAmbassadorProgramFactory ambassadorFactory)
        {
            _tenantProvider = tenantProvider;
            _loyaltyProgramService = loyaltyProgramService;
            _ambassadorFactory = ambassadorFactory;
        }
        [HttpGet]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Policy = "SuperAdminOnly")]
        [ProducesResponseType(typeof(ServiceResult<List<GetLoyaltyProgramDto>>), (int)HttpStatusCode.OK)]
        [ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.NotFound)]
        [ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.Unauthorized)]
        [ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.BadRequest)]
        public async Task<IActionResult> List()
        {
            var result = await _loyaltyProgramService.GetAll();
            return Ok(result);
        }

        /// <summary>Cari tenant-ın aktiv proqramları (tenant admin — məs. ambassador dialoqu üçün).</summary>
        [HttpGet]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Policy = "TenantAdminOrModerator")]
        [ProducesResponseType(typeof(ServiceResult<List<GetLoyaltyProgramDto>>), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> MyList()
        {
            var tenantId = _tenantProvider.CurrentTenantId;
            if (!tenantId.HasValue)
                return Ok(new ServiceResult(false, "Tenant not found."));
            var result = await _loyaltyProgramService.GetActiveByTenantAsync(tenantId.Value);
            return Ok(result);
        }
        [HttpPost]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Policy = "SuperAdminOnly")]
        [ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.OK)]
        [ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.NotFound)]
        [ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.Unauthorized)]
        [ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.BadRequest)]
        public async Task<IActionResult> Add([FromForm] AddLoyaltyProgramDto addTenantDto, CancellationToken ct)
        {
            if (!ModelState.IsValid)
            {

                foreach (var error in ModelState.Values.SelectMany(modelState => modelState.Errors))
                {
                    ModelState.AddModelError("", error.ErrorMessage);
                }
                return BadRequest(ModelState);
            }
            var result = await _loyaltyProgramService.AddOrUpdateAsync(addTenantDto, ct);
            if (result.Success)
                return Ok(result);

            //ModelState.AddModelError("", "Butun hisselerin tam doldurulduqundan emin olun!");
            return BadRequest(result);
        }
        [HttpPost]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Policy = "SuperAdminOnly")]
        [ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.OK)]
        [ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.NotFound)]
        [ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.Unauthorized)]
        [ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.BadRequest)]
        public async Task<IActionResult> Update([FromForm] AddLoyaltyProgramDto addTenantDto, int id, CancellationToken ct)
        {
            if (!ModelState.IsValid)
            {

                foreach (var error in ModelState.Values.SelectMany(modelState => modelState.Errors))
                {
                    ModelState.AddModelError("", error.ErrorMessage);
                }
                return BadRequest(ModelState);
            }
            var result = await _loyaltyProgramService.AddOrUpdateAsync(addTenantDto, ct);
            if (result.Success)
                return Ok(result);

            //ModelState.AddModelError("", "Butun hisselerin tam doldurulduqundan emin olun!");
            return BadRequest(result);
        }

        [HttpDelete]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Policy = "SuperAdminOnly")]
        [ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.OK)]
        [ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.NotFound)]
        [ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.Unauthorized)]
        [ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.BadRequest)]
        public async Task<IActionResult> Delete(int id)
        {
            var result = await _loyaltyProgramService.Delete(id);
            if (result.Success)
                return Ok(result);
            return BadRequest(result);
        }

        [HttpGet]
        [ProducesResponseType(typeof(ServiceResult<GetLoyaltyProgramDto>), (int)HttpStatusCode.OK)]
        [ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.NotFound)]
        public async Task<IActionResult> Get(int id)
        {
            var result = await _loyaltyProgramService.Get(id);
            if (!result.Success)
                return NotFound(result);
            // Super Admin deyilsə yalnız öz tenant-ının proqramı
            if (!User.IsInRole("Super Admin")
                && result.Data is Business.Dtos.Get.Main.GetLoyaltyProgramDto dto
                && dto.TenantId != _tenantProvider.CurrentTenantId)
                return NotFound(new ServiceResult(false, "Loyalty program not found."));
            return Ok(result);
        }

        [HttpPost]
        [ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.OK)]
        [ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.BadRequest)]
        public async Task<IActionResult> UpdateHappyHours(int id, [FromBody] UpdateHappyHoursDto dto, CancellationToken ct)
        {
            // Super Admin hər proqramı, digər rollar yalnız öz tenant-ının proqramını
            int? ownership = User.IsInRole("Super Admin") ? null : _tenantProvider.CurrentTenantId ?? -1;
            var result = await _loyaltyProgramService.UpdateHappyHoursAsync(id, dto, ownership, ct);
            if (result.Success)
                return Ok(result);
            return BadRequest(result);
        }

        /// <summary>Proqramın iiko bağlantısı (cüzdan, qazanc mənbəyi, sifariş başına möhür). Super Admin hər proqramı, Tenant Admin öz proqramını.</summary>
        [HttpPost]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Policy = "TenantAdminOrSuperAdmin")]
        [ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.OK)]
        [ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.BadRequest)]
        public async Task<IActionResult> UpdatePosLink(int id, [FromBody] UpdatePosLinkDto dto, CancellationToken ct)
        {
            int? ownership = null;
            if (!User.IsInRole("Super Admin"))
            {
                ownership = _tenantProvider.CurrentTenantId;
                if (!ownership.HasValue)
                    return BadRequest(new ServiceResult(false, "Tenant not found."));
            }

            var result = await _loyaltyProgramService.UpdatePosLinkAsync(id, dto, ownership, ct);
            if (result.Success)
                return Ok(result);
            return BadRequest(result);
        }

        /// <summary>
        /// Ambassador kartı yarat: ayrıca ambassador proqramı + adi kartın dizaynından kopya şablon (sonra «Kart dizaynı»nda dəyişdirilir).
        /// Tenant admin öz tenant-ı üçün; super admin ?tenantId= ilə.
        /// </summary>
        [HttpPost]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Policy = "TenantAdminOrSuperAdmin")]
        [ProducesResponseType(typeof(ServiceResult<CreateAmbassadorProgramResultDto>), (int)HttpStatusCode.OK)]
        [ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.BadRequest)]
        public async Task<IActionResult> CreateAmbassador([FromBody] CreateAmbassadorProgramDto dto, [FromQuery] int? tenantId, CancellationToken ct)
        {
            var tid = this.ScopedTenantId(tenantId, _tenantProvider);
            if (!tid.HasValue || tid <= 0)
                return BadRequest(new ServiceResult(false, "Tenant not found."));
            var userId = int.TryParse(_tenantProvider.CurrentUserId, out var uid) ? uid : 1;

            var result = await _ambassadorFactory.CreateForTenantAsync(tid.Value, userId, dto.Kind, dto.TotalSteps, dto.Name, ct);
            if (result.Success)
                return Ok(result);
            return BadRequest(result);
        }

        /// <summary>Xoş gəldin bonusu ayarları. Super Admin hər proqramı, Tenant Admin yalnız öz tenant-ının proqramını dəyişə bilər.</summary>
        [HttpPost]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Policy = "TenantAdminOrSuperAdmin")]
        [ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.OK)]
        [ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.BadRequest)]
        public async Task<IActionResult> UpdateWelcomeBonus(int id, [FromBody] UpdateWelcomeBonusDto dto, CancellationToken ct)
        {
            int? ownership = null;
            if (!User.IsInRole("Super Admin"))
            {
                ownership = _tenantProvider.CurrentTenantId;
                if (!ownership.HasValue)
                    return BadRequest(new ServiceResult(false, "Tenant not found."));
            }

            var result = await _loyaltyProgramService.UpdateWelcomeBonusAsync(id, dto, ownership, ct);
            if (result.Success)
                return Ok(result);
            return BadRequest(result);
        }
    }
}

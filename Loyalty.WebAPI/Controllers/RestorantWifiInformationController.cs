using Loyalty.Business.Dtos.Get.Main;
using Loyalty.Business.Dtos.Post.Main;
using Loyalty.Business.Results;
using Loyalty.Business.Services.Abstractions.Main;
using Loyalty.DataAccess.Context;
using Loyalty.WebAPI.Tenancy;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

namespace Loyalty.WebAPI.Controllers
{
    [Route("api/[controller]/[action]")]
    [ApiController]
    public class RestorantWifiInformationController : ControllerBase
    {
        private readonly IRestorantWifiInformationService _restorantWifiInformationService;
        private readonly ITenantProvider _tenantProvider;
        private readonly LoyaltyWalletDbContext _db;

        public RestorantWifiInformationController(
            IRestorantWifiInformationService restorantWifiInformationService,
            ITenantProvider tenantProvider,
            LoyaltyWalletDbContext db)
        {
            _restorantWifiInformationService = restorantWifiInformationService;
            _tenantProvider = tenantProvider;
            _db = db;
        }

        private bool IsBranchScoped()
            => !User.IsInRole("Tenant Admin")
               && !User.IsInRole("Super Admin")
               && (User.IsInRole("Branch Admin")
                   || User.IsInRole("Branch Moderator")
                   || User.IsInRole("Moderator")
                   // Menu Admin: branch_id claim-i varsa filiala kilidli, yoxdursa tenant-wide
                   || (User.IsInRole("Menu Admin") && _tenantProvider.CurrentBranchId.HasValue));

        [HttpGet]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        [ProducesResponseType(typeof(ServiceResult<List<GetRestorantWifiInformationDto>>), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> List([FromQuery] int? tenantId = null, [FromQuery] int? branchId = null)
        {
            var resolvedTenantId = tenantId ?? _tenantProvider.CurrentTenantId;
            if (!resolvedTenantId.HasValue)
                return BadRequest(new ServiceResult(false, "Tenant Id təyin olunmayıb"));

            var branchScoped = IsBranchScoped();
            var effectiveBranchId = branchScoped ? _tenantProvider.CurrentBranchId : branchId;

            var result = await _restorantWifiInformationService.GetByTenantAndBranchAsync(resolvedTenantId.Value, effectiveBranchId, branchScoped);
            if (result.Success) return Ok(result);
            return BadRequest(result);
        }

        [HttpGet]
        [ProducesResponseType(typeof(ServiceResult<List<GetRestorantWifiInformationDto>>), (int)HttpStatusCode.OK)]
        [ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.BadRequest)]
        public async Task<IActionResult> GetAll()
        {
            var result = await _restorantWifiInformationService.GetAll();
            if (result.Success)
                return Ok(result);
            
            return BadRequest(result);
        }

        [HttpGet]
        [ProducesResponseType(typeof(ServiceResult<List<GetRestorantWifiInformationDto>>), (int)HttpStatusCode.OK)]
        [ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.BadRequest)]
        public async Task<IActionResult> GetAllWithTenant()
        {
            var result = await _restorantWifiInformationService.GetAllWithTenant();
            if (result.Success)
                return Ok(result);
            
            return BadRequest(result);
        }

        [HttpGet("{id}")]
        [ProducesResponseType(typeof(ServiceResult<GetRestorantWifiInformationDto>), (int)HttpStatusCode.OK)]
        [ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.NotFound)]
        [ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.BadRequest)]
        public async Task<IActionResult> GetById(int id)
        {
            var result = await _restorantWifiInformationService.GetById(id);
            if (result.Success)
                return Ok(result);
            
            return NotFound(result);
        }

        [HttpGet("{id}")]
        [ProducesResponseType(typeof(ServiceResult<GetRestorantWifiInformationDto>), (int)HttpStatusCode.OK)]
        [ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.NotFound)]
        [ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.BadRequest)]
        public async Task<IActionResult> GetByIdWithTenant(int id)
        {
            var result = await _restorantWifiInformationService.GetByIdWithTenant(id);
            if (result.Success)
                return Ok(result);
            
            return NotFound(result);
        }

        [HttpGet("{tenantId}")]
        [ProducesResponseType(typeof(ServiceResult<List<GetRestorantWifiInformationDto>>), (int)HttpStatusCode.OK)]
        [ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.BadRequest)]
        public async Task<IActionResult> GetByTenantId(int tenantId)
        {
            var result = await _restorantWifiInformationService.GetByTenantId(tenantId);
            if (result.Success)
                return Ok(result);
            
            return BadRequest(result);
        }

        [HttpPost]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        [ProducesResponseType(typeof(ServiceResult<GetRestorantWifiInformationDto>), (int)HttpStatusCode.OK)]
        [ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.BadRequest)]
        [ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.Unauthorized)]
        public async Task<IActionResult> Add([FromBody] AddRestorantWifiInformationDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            if (IsBranchScoped())
            {
                var ownBranchId = _tenantProvider.CurrentBranchId;
                if (!ownBranchId.HasValue)
                    return StatusCode((int)HttpStatusCode.Forbidden, new ServiceResult(false, "Branch təyin olunmayıb"));
                dto.BranchId = ownBranchId;
            }

            var result = await _restorantWifiInformationService.Add(dto);
            if (result.Success)
                return Ok(result);

            return BadRequest(result);
        }

        [HttpPut]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        [ProducesResponseType(typeof(ServiceResult<GetRestorantWifiInformationDto>), (int)HttpStatusCode.OK)]
        [ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.NotFound)]
        [ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.BadRequest)]
        [ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.Unauthorized)]
        public async Task<IActionResult> Update([FromBody] UpdateRestorantWifiInformationDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            if (IsBranchScoped())
            {
                var ownBranchId = _tenantProvider.CurrentBranchId;
                var existing = await _db.RestorantWifiInformations.AsNoTracking()
                    .Where(w => w.Id == dto.Id)
                    .Select(w => new { w.BranchId })
                    .FirstOrDefaultAsync();
                if (existing == null) return NotFound(new ServiceResult(false, "WiFi tapılmadı"));
                if (existing.BranchId != ownBranchId)
                    return StatusCode((int)HttpStatusCode.Forbidden, new ServiceResult(false, "Bu WiFi-ni dəyişməyə icazəniz yoxdur"));
                dto.BranchId = ownBranchId;
            }

            var result = await _restorantWifiInformationService.Update(dto);
            if (result.Success)
                return Ok(result);

            return BadRequest(result);
        }

        [HttpDelete("{id}")]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        [ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.OK)]
        [ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.NotFound)]
        [ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.BadRequest)]
        [ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.Unauthorized)]
        public async Task<IActionResult> Delete(int id)
        {
            if (IsBranchScoped())
            {
                var ownBranchId = _tenantProvider.CurrentBranchId;
                var existing = await _db.RestorantWifiInformations.AsNoTracking()
                    .Where(w => w.Id == id)
                    .Select(w => new { w.BranchId })
                    .FirstOrDefaultAsync();
                if (existing == null) return NotFound(new ServiceResult(false, "WiFi tapılmadı"));
                if (existing.BranchId != ownBranchId)
                    return StatusCode((int)HttpStatusCode.Forbidden, new ServiceResult(false, "Bu WiFi-ni silməyə icazəniz yoxdur"));
            }

            var result = await _restorantWifiInformationService.Delete(id);
            if (result.Success)
                return Ok(result);

            return NotFound(result);
        }
    }
}

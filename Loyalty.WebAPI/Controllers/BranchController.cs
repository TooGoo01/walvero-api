using Loyalty.Business.Dtos.Post.Main.Branch;
using Loyalty.Business.Results;
using Loyalty.Business.Services.Abstractions.Main;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Net;

namespace Loyalty.WebAPI.Controllers
{
    [Route("api/[controller]/[action]")]
    [ApiController]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public class BranchController : ControllerBase
    {
        private readonly IBranchService _branchService;
        private readonly ITenantProvider _tenantProvider;

        public BranchController(IBranchService branchService, ITenantProvider tenantProvider)
        {
            _branchService = branchService;
            _tenantProvider = tenantProvider;
        }

        [HttpGet("/api/branch/list")]
        [ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> List()
        {
            int? tenantId = ResolveTenantId();
            var result = await _branchService.ListAsync(tenantId);
            return Ok(result);
        }

        [HttpGet("/api/branch/GetById")]
        [ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.OK)]
        [ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.NotFound)]
        public async Task<IActionResult> GetById([FromQuery] int id)
        {
            if (IsBranchScoped() && _tenantProvider.CurrentBranchId != id)
                return StatusCode((int)HttpStatusCode.Forbidden, new ServiceResult(false, "Bu branch-a icazəniz yoxdur"));

            var result = await _branchService.GetByIdAsync(id, OwnershipTenantId());
            if (!result.Success)
                return NotFound(result);
            return Ok(result);
        }

        [HttpPost("/api/branch/create")]
        [ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.OK)]
        [ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.BadRequest)]
        public async Task<IActionResult> Create([FromBody] CreateBranchDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            int? tenantId = ResolveTenantId();
            if (!tenantId.HasValue)
                return BadRequest(new ServiceResult(false, "Tenant context is required"));

            var result = await _branchService.CreateAsync(dto, tenantId.Value);
            if (!result.Success)
                return BadRequest(result);
            return Ok(result);
        }

        [HttpPost("/api/branch/Update")]
        [ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.OK)]
        [ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.NotFound)]
        public async Task<IActionResult> Update([FromQuery] int id, [FromBody] UpdateBranchDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            if (IsBranchScoped() && _tenantProvider.CurrentBranchId != id)
                return StatusCode((int)HttpStatusCode.Forbidden, new ServiceResult(false, "Bu branch-ı dəyişməyə icazəniz yoxdur"));

            var result = await _branchService.UpdateAsync(id, dto, OwnershipTenantId());
            if (!result.Success)
                return NotFound(result);
            return Ok(result);
        }

        [HttpPost("/api/branch/Delete")]
        [ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.OK)]
        [ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.NotFound)]
        public async Task<IActionResult> Delete([FromQuery] int id)
        {
            var result = await _branchService.DeleteAsync(id, OwnershipTenantId());
            if (!result.Success)
                return NotFound(result);
            return Ok(result);
        }

        [HttpGet("/api/branch/GetStats")]
        [ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.OK)]
        [ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.NotFound)]
        public async Task<IActionResult> GetStats([FromQuery] int branchId)
        {
            var result = await _branchService.GetStatsAsync(branchId, OwnershipTenantId());
            if (!result.Success)
                return NotFound(result);
            return Ok(result);
        }

        [HttpPost("/api/branch/UploadImage")]
        [RequestSizeLimit(20_000_000)]
        [RequestFormLimits(MultipartBodyLengthLimit = 20_000_000)]
        [ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.OK)]
        [ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.BadRequest)]
        public async Task<IActionResult> UploadImage()
        {
            var files = Request.Form.Files;
            if (files == null || files.Count == 0)
                return BadRequest(new ServiceResult(false, "No file provided"));

            var result = await _branchService.UploadImageAsync(files[0]);
            if (!result.Success)
                return BadRequest(result);
            return Ok(result);
        }

        [HttpPost("/api/branch/UploadBannerVideo")]
        [RequestSizeLimit(250_000_000)]
        [RequestFormLimits(MultipartBodyLengthLimit = 250_000_000)]
        [ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.OK)]
        [ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.BadRequest)]
        public async Task<IActionResult> UploadBannerVideo()
        {
            var files = Request.Form.Files;
            if (files == null || files.Count == 0)
                return BadRequest(new ServiceResult(false, "No file provided"));

            var result = await _branchService.UploadBannerVideoAsync(files[0]);
            if (!result.Success)
                return BadRequest(result);
            return Ok(result);
        }

        private int? ResolveTenantId()
        {
            // X-Tenant-Id header yalnız Super Admin üçün etibarlıdır — digər rollar
            // header spoofing ilə başqa tenant-ın kontekstinə keçə bilməsin.
            if (IsSuperAdmin() &&
                Request.Headers.TryGetValue("X-Tenant-Id", out var headerVal) &&
                int.TryParse(headerVal, out var headerId))
            {
                return headerId;
            }
            return _tenantProvider.CurrentTenantId;
        }

        private bool IsSuperAdmin() => User.IsInRole("Super Admin");

        // Superadmin bütün branch-lara toxuna bilər (null = ownership yoxlaması yoxdur);
        // digər rollar üçün öz tenant-ı ilə məhdudlaşır.
        private int? OwnershipTenantId() => IsSuperAdmin() ? null : ResolveTenantId();

        private bool IsBranchScoped()
            => !User.IsInRole("Tenant Admin")
               && !User.IsInRole("Super Admin")
               && (User.IsInRole("Branch Admin")
                   || User.IsInRole("Branch Moderator")
                   || User.IsInRole("Moderator")
                   // Menu Admin: branch_id claim-i varsa filiala kilidli, yoxdursa tenant-wide
                   || (User.IsInRole("Menu Admin") && _tenantProvider.CurrentBranchId.HasValue));
    }
}

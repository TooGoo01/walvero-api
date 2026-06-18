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
using System.Net;
using System.Threading.Tasks;

namespace Loyalty.WebAPI.Controllers
{
    [Route("api/[controller]/[action]")]
    [ApiController]
    public class MenuCategoryController : ControllerBase
    {
        private readonly IMenuCategoryService _menuCategoryService;
        private readonly ITenantProvider _tenantProvider;
        private readonly LoyaltyWalletDbContext _db;

        public MenuCategoryController(
            IMenuCategoryService menuCategoryService,
            ITenantProvider tenantProvider,
            LoyaltyWalletDbContext db)
        {
            _menuCategoryService = menuCategoryService;
            _tenantProvider = tenantProvider;
            _db = db;
        }

        private bool IsBranchScoped()
            => !User.IsInRole("Tenant Admin")
               && !User.IsInRole("Super Admin")
               && (User.IsInRole("Branch Admin")
                   || User.IsInRole("Branch Moderator")
                   || User.IsInRole("Moderator"));

        [HttpGet]
        [ProducesResponseType(typeof(ServiceResult<List<GetMenuCategoryDto>>), (int)HttpStatusCode.OK)]
        [ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.BadRequest)]
        public async Task<IActionResult> GetAll()
        {
            var result = await _menuCategoryService.GetAll();
            if (result.Success)
                return Ok(result);

            return BadRequest(result);
        }

        [HttpGet]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        [ProducesResponseType(typeof(ServiceResult<List<GetMenuCategoryDto>>), (int)HttpStatusCode.OK)]
        [ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.BadRequest)]
        public async Task<IActionResult> List([FromQuery] int? tenantId = null, [FromQuery] int? branchId = null)
        {
            var resolvedTenantId = tenantId ?? _tenantProvider.CurrentTenantId;
            if (!resolvedTenantId.HasValue)
                return BadRequest(new ServiceResult(false, "Tenant Id təyin olunmayıb"));

            var branchScoped = IsBranchScoped();
            var effectiveBranchId = branchScoped ? _tenantProvider.CurrentBranchId : branchId;

            var result = await _menuCategoryService.GetByTenantAndBranchAsync(resolvedTenantId.Value, effectiveBranchId, branchScoped);
            if (result.Success)
                return Ok(result);
            return BadRequest(result);
        }

        [HttpGet]
        [ProducesResponseType(typeof(ServiceResult<List<GetMenuCategoryDto>>), (int)HttpStatusCode.OK)]
        [ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.BadRequest)]
        public async Task<IActionResult> GetAllWithItems()
        {
            var result = await _menuCategoryService.GetAllWithItems();
            if (result.Success)
                return Ok(result);

            return BadRequest(result);
        }

        [HttpGet]
        [ProducesResponseType(typeof(ServiceResult<List<GetMenuCategoryDto>>), (int)HttpStatusCode.OK)]
        [ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.BadRequest)]
        public async Task<IActionResult> GetActive()
        {
            var result = await _menuCategoryService.GetActive();
            if (result.Success)
                return Ok(result);

            return BadRequest(result);
        }

        [HttpGet("{id}")]
        [ProducesResponseType(typeof(ServiceResult<GetMenuCategoryDto>), (int)HttpStatusCode.OK)]
        [ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.NotFound)]
        [ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.BadRequest)]
        public async Task<IActionResult> GetById(int id)
        {
            var result = await _menuCategoryService.GetById(id);
            if (result.Success)
                return Ok(result);

            return NotFound(result);
        }

        [HttpGet("{id}")]
        [ProducesResponseType(typeof(ServiceResult<GetMenuCategoryDto>), (int)HttpStatusCode.OK)]
        [ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.NotFound)]
        [ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.BadRequest)]
        public async Task<IActionResult> GetByIdWithItems(int id)
        {
            var result = await _menuCategoryService.GetByIdWithItems(id);
            if (result.Success)
                return Ok(result);

            return NotFound(result);
        }

        [HttpPost]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        [ProducesResponseType(typeof(ServiceResult<GetMenuCategoryDto>), (int)HttpStatusCode.OK)]
        [ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.BadRequest)]
        [ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.Unauthorized)]
        public async Task<IActionResult> Add([FromBody] AddMenuCategoryDto dto)
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

            var result = await _menuCategoryService.Add(dto);
            if (result.Success)
                return Ok(result);

            return BadRequest(result);
        }

        [HttpPut]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        [ProducesResponseType(typeof(ServiceResult<GetMenuCategoryDto>), (int)HttpStatusCode.OK)]
        [ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.NotFound)]
        [ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.BadRequest)]
        [ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.Unauthorized)]
        public async Task<IActionResult> Update([FromBody] UpdateMenuCategoryDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            if (IsBranchScoped())
            {
                var ownBranchId = _tenantProvider.CurrentBranchId;
                var existing = await _db.MenuCategories.AsNoTracking()
                    .Where(mc => mc.Id == dto.Id)
                    .Select(mc => new { mc.BranchId })
                    .FirstOrDefaultAsync();
                if (existing == null) return NotFound(new ServiceResult(false, "Menyu kateqoriyası tapılmadı"));
                if (existing.BranchId != ownBranchId)
                    return StatusCode((int)HttpStatusCode.Forbidden, new ServiceResult(false, "Bu kateqoriyanı dəyişməyə icazəniz yoxdur"));
                dto.BranchId = ownBranchId;
            }

            var result = await _menuCategoryService.Update(dto);
            if (result.Success)
                return Ok(result);

            return BadRequest(result);
        }

        [HttpPost]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        [ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.OK)]
        [ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.BadRequest)]
        public async Task<IActionResult> Reorder([FromBody] ReorderMenuCategoryDto dto)
        {
            if (dto.TenantId <= 0)
                dto.TenantId = _tenantProvider.CurrentTenantId ?? 0;
            if (dto.TenantId <= 0)
                return BadRequest(new ServiceResult(false, "Tenant Id təyin olunmayıb"));

            if (IsBranchScoped())
            {
                var own = _tenantProvider.CurrentBranchId;
                if (!own.HasValue)
                    return StatusCode((int)HttpStatusCode.Forbidden, new ServiceResult(false, "Branch təyin olunmayıb"));
                dto.BranchId = own;
            }

            var result = await _menuCategoryService.ReorderAsync(dto);
            return result.Success ? Ok(result) : BadRequest(result);
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
                var existing = await _db.MenuCategories.AsNoTracking()
                    .Where(mc => mc.Id == id)
                    .Select(mc => new { mc.BranchId })
                    .FirstOrDefaultAsync();
                if (existing == null) return NotFound(new ServiceResult(false, "Menyu kateqoriyası tapılmadı"));
                if (existing.BranchId != ownBranchId)
                    return StatusCode((int)HttpStatusCode.Forbidden, new ServiceResult(false, "Bu kateqoriyanı silməyə icazəniz yoxdur"));
            }

            var result = await _menuCategoryService.Delete(id);
            if (result.Success)
                return Ok(result);

            return NotFound(result);
        }
    }
}

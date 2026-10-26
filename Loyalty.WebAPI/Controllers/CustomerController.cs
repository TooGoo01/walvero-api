using Loyalty.Business.Dtos.Get.Main;
using Loyalty.Business.Dtos.Post.Main;
using Loyalty.Business.Dtos.Post.Users;
using Loyalty.Business.Results;
using Loyalty.Business.Services.Abstractions.Main;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Loyalty.WebAPI.Tenancy;
using System.Net;

namespace Loyalty.WebAPI.Controllers
{
    [Route("api/[controller]/[action]")]
    [ApiController]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public class CustomerController : ControllerBase
    {
        private readonly ICustomerEnrollmentService _customerEnrollmentService;
        private readonly ICustomerGuestCategoryService _guestCategoryService;
        private readonly ITenantProvider _tenantProvider;
        public CustomerController(ICustomerEnrollmentService customerEnrollmentService, ICustomerGuestCategoryService guestCategoryService, ITenantProvider tenantProvider)
        {
            _customerEnrollmentService = customerEnrollmentService;
            _guestCategoryService = guestCategoryService;
            _tenantProvider = tenantProvider;
        }

        [HttpPost]
        [Authorize(Policy = "CustomerOnly")]
        [ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.OK)]

        public async Task<ActionResult<ServiceResult>> Add([FromBody] AddCustomerWithCardDto model)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(new ServiceResult(false, "Invalid request data"));
            }

            if (model == null)
            {
                return BadRequest(new ServiceResult(false, "Request body is required"));
            }

            if (model.TenantId <= 0)
            {
                return BadRequest(new ServiceResult(false, "TenantId is required and must be greater than 0"));
            }

            if (model.LoyaltyProgramId <= 0)
            {
                return BadRequest(new ServiceResult(false, "LoyaltyProgramId is required and must be greater than 0"));
            }

            try
            {
                var result = await _customerEnrollmentService.StartEnrollmentAsync(model);
                if (result.Success)
                {
                    return Ok(new ServiceResult(true, result.Data, "Adding completed"));
                }
                return BadRequest(new ServiceResult(false, result.Message));
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ServiceResult(false, $"Server error: {ex.Message}"));
            }
        }
        [HttpGet]
        [Authorize(Policy = "TenantAdminOrModerator")]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Policy = "TenantAdminOrModerator")]
        [ProducesResponseType(typeof(ServiceResult<List<GetCustomerListItemDto>>), (int)HttpStatusCode.OK)]
        [ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.NotFound)]
        [ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.Unauthorized)]
        [ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.BadRequest)]
        public async Task<IActionResult> List()
        {
            var tenantId = _tenantProvider.CurrentTenantId;
            if (!tenantId.HasValue)
                return BadRequest(new ServiceResult(false, "Tenant not found."));

            var result = await _customerEnrollmentService.GetTenantCustomers(tenantId.Value, "");
            return Ok(result);
        }

        /// <summary>
        /// Kassa endirim səviyyəsi: müştərini iiko endirim kampaniyasının qonaq kateqoriyasına yaz (categoryId boş = sil).
        /// Səviyyələr kataloqdan gəlir (GET tenant-integrations/{id}/iiko/catalog → discountTiers).
        /// </summary>
        [HttpPost]
        [Authorize(Policy = "TenantAdminOrModerator")]
        [ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.OK)]
        [ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.BadRequest)]
        public async Task<IActionResult> SetPosCategory([FromBody] SetCustomerPosCategoryDto model, [FromQuery] int? tenantId, CancellationToken ct)
        {
            if (!ModelState.IsValid || model == null)
                return BadRequest(new ServiceResult(false, "Invalid request data"));

            var tid = this.ScopedTenantId(tenantId, _tenantProvider);
            if (!tid.HasValue || tid <= 0)
                return BadRequest(new ServiceResult(false, "Tenant not found."));

            var result = await _guestCategoryService.SetAsync(tid.Value, model, ct);
            return result.Success ? Ok(result) : BadRequest(result);
        }

    }
}

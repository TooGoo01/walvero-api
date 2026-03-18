using Loyalty.Business.Dtos.Get.Main;
using Loyalty.Business.Dtos.Post.Main;
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
    public class TransactionController : ControllerBase
    {
        private readonly ITransactionService _transactionService;
        private readonly ITransactionReverseService _transactionReverseService;
        private readonly ITenantProvider _tenantProvider;

        public TransactionController(
            ITransactionService transactionService,
            ITransactionReverseService transactionReverseService,
            ITenantProvider tenantProvider)
        {
            _transactionService = transactionService;
            _transactionReverseService = transactionReverseService;
            _tenantProvider = tenantProvider;
        }

        [HttpGet]
        [Authorize(Policy = "TenantAdminOrModerator")]
        [ProducesResponseType(typeof(ServiceResult<List<GetTransactionDto>>), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> List(DateOnly? startDate, DateOnly? endDate, int? tenantId = null)
        {
            var today = DateOnly.FromDateTime(DateTime.Today);
            var effectiveEndDate = endDate ?? today;
            var effectiveStartDate = startDate ?? effectiveEndDate.AddDays(-30);

            var id = tenantId ?? _tenantProvider.CurrentTenantId ?? 0;
            if (id <= 0)
                return BadRequest(new ServiceResult(false, "TenantId is required"));

            var result = await _transactionService.GetByTenantIdAndDate(id, effectiveStartDate, effectiveEndDate);
            return Ok(result);
        }

        [HttpGet]
        [Authorize(Policy = "TenantAdminOrModerator")]
        [ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> ByCustomer([FromQuery] int customerId)
        {
            if (customerId <= 0)
                return BadRequest(new ServiceResult(false, "CustomerId is required"));

            var result = await _transactionService.GetByCustomerId(customerId);
            return Ok(result);
        }

        [HttpPost]
        [Authorize(Policy = "TenantAdminOrModerator")]
        [ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> Reverse([FromBody] ReverseTransactionDto dto, CancellationToken ct)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var result = await _transactionReverseService.ReverseTransactionAsync(dto, ct);
            return Ok(result);
        }
    }
}

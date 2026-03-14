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
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Policy = "TenantAdminOrModerator")]
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
        [ProducesResponseType(typeof(ServiceResult<List<GetTransactionDto>>), (int)HttpStatusCode.OK)]
        [ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.BadRequest)]
        public async Task<IActionResult> List(DateOnly? startDate, DateOnly? endDate, int? tenantId = null)
        {
            var today = DateOnly.FromDateTime(DateTime.Today);

            DateOnly effectiveEndDate;
            DateOnly effectiveStartDate;

            if (!startDate.HasValue && !endDate.HasValue)
            {
                effectiveEndDate = today;
                effectiveStartDate = today.AddDays(-30);
            }
            else
            {
                effectiveEndDate = endDate ?? today;
                effectiveStartDate = startDate ?? effectiveEndDate.AddDays(-30);
            }

            var effectiveTenantId = (tenantId.HasValue && tenantId.Value > 0)
                ? tenantId.Value
                : _tenantProvider.CurrentTenantId;

            if (!effectiveTenantId.HasValue)
                return BadRequest(new ServiceResult(false, "Tenant not found."));

            var result = await _transactionService.GetByTenantIdAndDate(
                effectiveTenantId.Value,
                effectiveStartDate,
                effectiveEndDate
            );
            return Ok(result);
        }

        [HttpGet]
        [ProducesResponseType(typeof(ServiceResult<List<GetTransactionDto>>), (int)HttpStatusCode.OK)]
        [ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.BadRequest)]
        public async Task<IActionResult> ByCustomer(int customerId)
        {
            if (customerId <= 0)
                return BadRequest(new ServiceResult(false, "Customer ID is required."));

            var result = await _transactionService.GetByCustomerId(customerId);
            return Ok(result);
        }

        [HttpPost]
        [ProducesResponseType(typeof(ServiceResult<ReverseTransactionResponseDto>), (int)HttpStatusCode.OK)]
        [ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.BadRequest)]
        public async Task<IActionResult> Reverse([FromBody] ReverseTransactionDto dto, CancellationToken ct)
        {
            var result = await _transactionReverseService.ReverseTransactionAsync(dto, ct);
            if (!result.Success)
                return BadRequest(result);

            return Ok(result);
        }
    }
}

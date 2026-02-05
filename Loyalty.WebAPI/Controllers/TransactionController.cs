using Loyalty.Business.Dtos.Get.Main;
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
    public class TransactionController : ControllerBase
    {
        private readonly ITransactionService _transactionService;

        public TransactionController(ITransactionService transactionService)
        {
            _transactionService = transactionService;
        }
        [HttpGet]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Policy = "TenantAdminOnly")]
        [ProducesResponseType(typeof(ServiceResult<List<GetTransactionDto>>), (int)HttpStatusCode.OK)]
        [ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.NotFound)]
        [ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.Unauthorized)]
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

           

            if (tenantId.HasValue && tenantId.Value > 0)
            {
                var result = await _transactionService.GetByTenantIdAndDate(
                    tenantId.Value,
                    effectiveStartDate,
                    effectiveEndDate
                );
                return Ok(result);
            }

            return BadRequest(false);
        }
    }
}

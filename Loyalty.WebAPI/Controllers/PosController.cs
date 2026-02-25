using Loyalty.Business.Dtos.Post.Main;
using Loyalty.Business.Results;
using Loyalty.Business.Services.Abstractions.Main;
using Loyalty.WebAPI.Tenancy;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Net;

namespace Loyalty.WebAPI.Controllers
{
    [Route("api/pos")]
    [ApiController]
    [Authorize(
        AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme,
        Policy = "TenantAdminOrModerator")]
    public class PosController : ControllerBase
    {
        private readonly ITransactionReverseService _transactionReverseService;
        private readonly IPosSessionService _posSessionService;
        private readonly ICustomerSyncService _customerSyncService;
        private readonly ITenantProvider _tenantProvider;
        private readonly ILogger<PosController> _logger;

        public PosController(
            ITransactionReverseService transactionReverseService,
            IPosSessionService posSessionService,
            ICustomerSyncService customerSyncService,
            ITenantProvider tenantProvider,
            ILogger<PosController> logger)
        {
            _transactionReverseService = transactionReverseService;
            _posSessionService = posSessionService;
            _customerSyncService = customerSyncService;
            _tenantProvider = tenantProvider;
            _logger = logger;
        }

        /// <summary>
        /// Reverse a loyalty transaction
        /// </summary>
        [HttpPost("loyalty/reverse")]
        [ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> ReverseTransaction([FromBody] ReverseTransactionDto dto, CancellationToken ct)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            _logger.LogInformation(
                "Reversing transaction. TransactionId={TransactionId}, BillId={BillId}, OriginalType={OriginalType}",
                dto.TransactionId, dto.OrderId, dto.OriginalType);

            var result = await _transactionReverseService.ReverseTransactionAsync(dto, ct);

            if (result.Success)
            {
                return Ok(result);
            }

            return BadRequest(result);
        }

        /// <summary>
        /// Link a POS session with an order
        /// </summary>
        [HttpPost("session/link")]
        [ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> LinkSession([FromBody] LinkSessionDto dto, CancellationToken ct)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            _logger.LogInformation(
                "Linking session. OrderId={OrderId}, SessionToken={SessionToken}",
                dto.OrderId, dto.SessionToken);

            var result = await _posSessionService.LinkSessionAsync(dto, ct);

            if (result.Success)
            {
                return Ok(result);
            }

            return BadRequest(result);
        }

        /// <summary>
        /// Sync all customers for the current tenant to Monyo POS.
        /// </summary>
        [HttpPost("sync-customers")]
        [ProducesResponseType(typeof(object), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> SyncAllCustomers([FromQuery] int? tenantId, CancellationToken ct)
        {
            var tid = tenantId ?? _tenantProvider.CurrentTenantId;
            if (!tid.HasValue || tid <= 0)
                return BadRequest("TenantId required.");
            var count = await _customerSyncService.SyncAllCustomersToPosAsync(tid.Value, ct);
            return Ok(new { syncedCount = count });
        }

        /// <summary>
        /// Sync a single customer to Monyo POS.
        /// </summary>
        [HttpPost("sync-customers/{customerId:int}")]
        [ProducesResponseType(typeof(object), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> SyncCustomer(int customerId, [FromQuery] int? tenantId, CancellationToken ct)
        {
            var tid = tenantId ?? _tenantProvider.CurrentTenantId;
            if (!tid.HasValue || tid <= 0)
                return BadRequest("TenantId required.");
            var result = await _customerSyncService.SyncCustomerToPosAsync(tid.Value, customerId, ct);
            if (!result.Success) return BadRequest("Sync failed.");
            return Ok(new { success = true, walveroId = result.WalveroId, barcode = result.Barcode });
        }
    }
}


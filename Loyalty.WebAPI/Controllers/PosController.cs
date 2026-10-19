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
        private readonly ITenantStatusGuard _tenantStatusGuard;
        private readonly IPosCustomerIikoService _posCustomerIiko;
        private readonly IPosCustomerImportService _posCustomerImport;
        private readonly global::Hangfire.IBackgroundJobClient _jobClient;
        private readonly ILogger<PosController> _logger;

        public PosController(
            ITransactionReverseService transactionReverseService,
            IPosSessionService posSessionService,
            ICustomerSyncService customerSyncService,
            ITenantProvider tenantProvider,
            ITenantStatusGuard tenantStatusGuard,
            IPosCustomerIikoService posCustomerIiko,
            IPosCustomerImportService posCustomerImport,
            global::Hangfire.IBackgroundJobClient jobClient,
            ILogger<PosController> logger)
        {
            _jobClient = jobClient;
            _posCustomerIiko = posCustomerIiko;
            _posCustomerImport = posCustomerImport;
            _transactionReverseService = transactionReverseService;
            _posSessionService = posSessionService;
            _customerSyncService = customerSyncService;
            _tenantProvider = tenantProvider;
            _tenantStatusGuard = tenantStatusGuard;
            _logger = logger;
        }

        /// <summary>Deaktiv tenant-da POS əməliyyatları qadağandır → 403.</summary>
        private async Task<IActionResult?> BlockIfTenantInactiveAsync(int? tenantId, CancellationToken ct)
        {
            if (tenantId.HasValue && !await _tenantStatusGuard.IsActiveAsync(tenantId.Value, ct))
            {
                return StatusCode((int)HttpStatusCode.Forbidden,
                    new ServiceResult(false, Business.Services.Implementations.Main.TenantStatusGuard.InactiveMessage));
            }
            return null;
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

            var blocked = await BlockIfTenantInactiveAsync(_tenantProvider.CurrentTenantId, ct);
            if (blocked != null) return blocked;

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
            var tid = this.ScopedTenantId(tenantId, _tenantProvider);
            if (!tid.HasValue || tid <= 0)
                return BadRequest("TenantId required.");
            var blocked = await BlockIfTenantInactiveAsync(tid, ct);
            if (blocked != null) return blocked;
            var count = await _customerSyncService.SyncAllCustomersToPosAsync(tid.Value, ct);
            return Ok(new { syncedCount = count });
        }

        /// <summary>
        /// Bütün müştəriləri POS-a arxa planda (Hangfire) sinxron et — kart track rejimi dəyişəndən sonra
        /// hər müştəriyə qısa/əlavə track yazılsın. Sinxron variant (sync-customers) minlərlə iiko çağırışında timeout verir.
        /// </summary>
        [HttpPost("sync-customers/enqueue")]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Policy = "SuperAdminOnly")]
        public async Task<IActionResult> EnqueueSyncAllCustomers([FromQuery] int tenantId, CancellationToken ct)
        {
            if (tenantId <= 0) return BadRequest("TenantId required.");
            var blocked = await BlockIfTenantInactiveAsync(tenantId, ct);
            if (blocked != null) return blocked;
            var jobId = global::Hangfire.BackgroundJobClientExtensions.Enqueue<ICustomerSyncService>(
                _jobClient, s => s.SyncAllCustomersToPosAsync(tenantId, CancellationToken.None));
            _logger.LogInformation("POS sync-all enqueued. TenantId={TenantId}, JobId={JobId}", tenantId, jobId);
            return Ok(new { jobId });
        }

        /// <summary>
        /// Sync a single customer to Monyo POS.
        /// </summary>
        [HttpPost("sync-customers/{customerId:int}")]
        [ProducesResponseType(typeof(object), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> SyncCustomer(int customerId, [FromQuery] int? tenantId, CancellationToken ct)
        {
            var tid = this.ScopedTenantId(tenantId, _tenantProvider);
            if (!tid.HasValue || tid <= 0)
                return BadRequest("TenantId required.");
            var blocked = await BlockIfTenantInactiveAsync(tid, ct);
            if (blocked != null) return blocked;
            var result = await _customerSyncService.SyncCustomerToPosAsync(tid.Value, customerId, ct);
            if (!result.Success) return BadRequest("Sync failed.");
            return Ok(new { success = true, walveroId = result.WalveroId, barcode = result.Barcode });
        }

        /// <summary>Müştərinin iiko vəziyyəti (hər venue/org üzrə): tapılırmı, wallet balansı, bizim balans, fərq.</summary>
        [HttpGet("customers/{customerId:int}/iiko-status")]
        [ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> GetCustomerIikoStatus(int customerId, [FromQuery] int? cardId, [FromQuery] int? tenantId, [FromQuery] string? lookupCardTrack, CancellationToken ct)
        {
            var tid = this.ScopedTenantId(tenantId, _tenantProvider);
            if (!tid.HasValue || tid <= 0)
                return BadRequest(new ServiceResult(false, "TenantId required."));
            var result = await _posCustomerIiko.GetStatusAsync(tid.Value, customerId, cardId, lookupCardTrack, ct);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        /// <summary>
        /// Kənar sistemdən (məs. PrimeHill) bir müştərinin köçürülməsi: müştəri + kart (Walvero nömrəsi) + açılış balansı (Adjusted).
        /// Idempotent (OrderRef). iiko sinxronu ayrıca: iiko-status / iiko-push-balance / sync-customers.
        /// </summary>
        [HttpPost("import-customer")]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Policy = "TenantAdminOrSuperAdmin")]
        [ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> ImportCustomer([FromBody] ImportCustomerDto dto, [FromQuery] int? tenantId, [FromQuery] bool dryRun, CancellationToken ct)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            var tid = this.ScopedTenantId(tenantId, _tenantProvider);
            if (!tid.HasValue || tid <= 0)
                return BadRequest(new ServiceResult(false, "TenantId required."));
            var blocked = await BlockIfTenantInactiveAsync(tid, ct);
            if (blocked != null) return blocked;
            var result = await _posCustomerImport.ImportAsync(tid.Value, dto, dryRun, ct);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        /// <summary>Walvero → iiko: iiko wallet-i bizim kart balansına bərabərləşdir (topup/chargeoff).</summary>
        [HttpPost("customers/{customerId:int}/iiko-push-balance")]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Policy = "TenantAdminOrSuperAdmin")]
        [ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> PushCustomerBalanceToIiko(int customerId, [FromBody] IikoPushBalanceDto dto, [FromQuery] int? tenantId, CancellationToken ct)
        {
            var tid = this.ScopedTenantId(tenantId, _tenantProvider);
            if (!tid.HasValue || tid <= 0)
                return BadRequest(new ServiceResult(false, "TenantId required."));
            var blocked = await BlockIfTenantInactiveAsync(tid, ct);
            if (blocked != null) return blocked;
            var result = await _posCustomerIiko.PushBalanceAsync(tid.Value, customerId, dto ?? new IikoPushBalanceDto(), ct);
            return result.Success ? Ok(result) : BadRequest(result);
        }

        /// <summary>iiko → Walvero: bizim kartı seçilmiş org-un wallet balansına bərabərləşdir (Adjusted tx + pass push).</summary>
        [HttpPost("customers/{customerId:int}/iiko-pull-balance")]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Policy = "TenantAdminOrSuperAdmin")]
        [ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> PullCustomerBalanceFromIiko(int customerId, [FromBody] IikoPullBalanceDto dto, [FromQuery] int? tenantId, CancellationToken ct)
        {
            var tid = this.ScopedTenantId(tenantId, _tenantProvider);
            if (!tid.HasValue || tid <= 0)
                return BadRequest(new ServiceResult(false, "TenantId required."));
            var blocked = await BlockIfTenantInactiveAsync(tid, ct);
            if (blocked != null) return blocked;
            var result = await _posCustomerIiko.PullBalanceAsync(tid.Value, customerId, dto, ct);
            return result.Success ? Ok(result) : BadRequest(result);
        }
    }
}


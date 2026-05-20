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
        public async Task<IActionResult> List(DateOnly? startDate, DateOnly? endDate, int? tenantId = null, int? branchId = null)
        {
            var today = DateOnly.FromDateTime(DateTime.Today);
            var effectiveEndDate = endDate ?? today;
            var effectiveStartDate = startDate ?? effectiveEndDate.AddDays(-30);

            var id = tenantId ?? _tenantProvider.CurrentTenantId ?? 0;
            if (id <= 0)
                return BadRequest(new ServiceResult(false, "TenantId is required"));

            // Branch Admin və Moderator yalnız öz branch tx-lərini görür. Tenant/Super Admin filter-i seçə bilər.
            int? effectiveBranchId = IsBranchAdmin() || IsModeratorOnly()
                ? _tenantProvider.CurrentBranchId
                : branchId;

            var result = await _transactionService.GetByTenantIdAndDate(id, effectiveStartDate, effectiveEndDate, effectiveBranchId);
            return Ok(result);
        }

        private bool IsModeratorOnly()
            => User.IsInRole("Moderator") && !User.IsInRole("Tenant Admin") && !User.IsInRole("Super Admin");

        [HttpGet]
        [Authorize(Policy = "TenantAdminOrModerator")]
        [ProducesResponseType(typeof(ServiceResult<List<GetTransactionDto>>), (int)HttpStatusCode.OK)]
        [ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.BadRequest)]
        public async Task<IActionResult> ByCustomer([FromQuery] int customerId)
        {
            if (customerId <= 0)
                return BadRequest(new ServiceResult(false, "CustomerId is required"));

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

        public sealed record ReverseLastByCardRequest(string CardNumber, string? Reason);

        [HttpPost]
        [ProducesResponseType(typeof(ServiceResult<ReverseTransactionResponseDto>), (int)HttpStatusCode.OK)]
        [ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.BadRequest)]
        public async Task<IActionResult> ReverseLastByCard([FromBody] ReverseLastByCardRequest req, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(req?.CardNumber))
                return BadRequest(new ServiceResult(false, "CardNumber is required"));

            var result = await _transactionReverseService.ReverseLastTransactionByCardAsync(req.CardNumber, req.Reason, ct);
            if (!result.Success)
                return BadRequest(result);

            return Ok(result);
        }

        // === Flagged transaction review (per-branch suspicious workflow) ===
        public sealed record ApproveFlaggedRequest(int TransactionId);
        public sealed record RejectFlaggedRequest(int TransactionId, string? Reason);

        [HttpPost]
        [Authorize(Policy = "BranchAdminOrAbove")]
        [ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> ApproveFlagged([FromBody] ApproveFlaggedRequest req, CancellationToken ct)
        {
            if (req.TransactionId <= 0)
                return BadRequest(new ServiceResult(false, "TransactionId is required"));

            // Branch-scoped admin yalnız öz filialının tx-lərini təsdiqləyə bilər. Tenant Admin / Super Admin = null.
            int? scopeBranchId = IsBranchAdmin() ? _tenantProvider.CurrentBranchId : null;

            var result = await _transactionReverseService.ApproveFlaggedAsync(req.TransactionId, scopeBranchId, ct);
            if (!result.Success)
                return BadRequest(result);
            return Ok(result);
        }

        [HttpPost]
        [Authorize(Policy = "BranchAdminOrAbove")]
        [ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> RejectFlagged([FromBody] RejectFlaggedRequest req, CancellationToken ct)
        {
            if (req.TransactionId <= 0)
                return BadRequest(new ServiceResult(false, "TransactionId is required"));

            int? scopeBranchId = IsBranchAdmin() ? _tenantProvider.CurrentBranchId : null;

            var result = await _transactionReverseService.RejectFlaggedAsync(req.TransactionId, scopeBranchId, req.Reason, ct);
            if (!result.Success)
                return BadRequest(result);
            return Ok(result);
        }

        private bool IsBranchAdmin()
            => User.IsInRole("Branch Admin") && !User.IsInRole("Tenant Admin") && !User.IsInRole("Super Admin");
    }
}

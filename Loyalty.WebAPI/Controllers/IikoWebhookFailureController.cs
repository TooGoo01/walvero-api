using Loyalty.Business.Results;
using Loyalty.Business.Services.Abstractions.Main;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Loyalty.WebAPI.Controllers
{
    /// <summary>
    /// Super admin: uğursuz iiko webhook sifarişlərinə baxış və əl ilə requeue.
    /// Status: 0 Pending, 1 Resolved, 2 Abandoned, 3 Rejected. Kind: 1 Transient, 2 Retriable, 3 Permanent.
    /// </summary>
    [Route("api/admin/iiko-webhook-failures")]
    [ApiController]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Policy = "SuperAdminOnly")]
    public class IikoWebhookFailureController : ControllerBase
    {
        private readonly IIikoWebhookFailureService _service;

        public IikoWebhookFailureController(IIikoWebhookFailureService service)
        {
            _service = service;
        }

        /// <summary>Siyahı (RawBody-siz). ?status=0&amp;terminalGroupId=…&amp;take=50</summary>
        [HttpGet]
        public async Task<IActionResult> List([FromQuery] int? status, [FromQuery] string? terminalGroupId, [FromQuery] int take = 50, CancellationToken ct = default)
        {
            var rows = await _service.ListAsync(status, terminalGroupId, take, ct);
            return Ok(ServiceResult.Ok(rows));
        }

        /// <summary>Tək sətir, orijinal payload ilə.</summary>
        [HttpGet("{id:int}")]
        public async Task<IActionResult> Get(int id, CancellationToken ct = default)
        {
            var row = await _service.GetAsync(id, ct);
            if (row == null) return NotFound(ServiceResult.Fail("Tapılmadı"));
            return Ok(ServiceResult.Ok(row));
        }

        /// <summary>Sətri dərhal növbəyə qaytar (AttemptCount sıfırlanır, növbəti retry dövrəsində emal olunur).</summary>
        [HttpPost("{id:int}/requeue")]
        public async Task<IActionResult> Requeue(int id, CancellationToken ct = default)
        {
            var ok = await _service.RequeueAsync(id, ct);
            if (!ok) return NotFound(ServiceResult.Fail("Tapılmadı"));
            return Ok(ServiceResult.Ok(null, "Növbəyə qaytarıldı"));
        }

        /// <summary>Terminal qrupunun bütün Pending/Abandoned/Rejected sətirlərini requeue et (yeni venue əlavə ediləndə).</summary>
        [HttpPost("requeue-by-terminal")]
        public async Task<IActionResult> RequeueByTerminal([FromQuery] string terminalGroupId, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(terminalGroupId))
                return BadRequest(ServiceResult.Fail("terminalGroupId tələb olunur"));
            var count = await _service.RequeueByTerminalGroupAsync(terminalGroupId, ct);
            return Ok(ServiceResult.Ok(new { requeued = count }));
        }
    }
}

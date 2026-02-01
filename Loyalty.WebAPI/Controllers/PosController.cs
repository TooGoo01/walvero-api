using Loyalty.Business.Dtos.Post.Main;
using Loyalty.Business.Results;
using Loyalty.Business.Services.Abstractions.Main;
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
        private readonly ILogger<PosController> _logger;

        public PosController(
            ITransactionReverseService transactionReverseService,
            IPosSessionService posSessionService,
            ILogger<PosController> logger)
        {
            _transactionReverseService = transactionReverseService;
            _posSessionService = posSessionService;
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
    }
}


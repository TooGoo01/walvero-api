using Loyalty.Business.Dtos.Post.Main;
using Loyalty.Business.Results;
using Loyalty.Business.Services.Abstractions.Main;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Net;

namespace Loyalty.WebAPI.Controllers
{
    [Route("api/[controller]/[action]")]
    [ApiController]
    [Authorize(
        AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme,
        Policy = "TenantAdminOrModerator")]
    public class LotteryController : ControllerBase
    {
        private readonly ILotteryService _lotteryService;
        private readonly ITenantProvider _tenantProvider;

        public LotteryController(ILotteryService lotteryService, ITenantProvider tenantProvider)
        {
            _lotteryService = lotteryService;
            _tenantProvider = tenantProvider;
        }

        [HttpGet]
        [ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> GetTickets([FromQuery] int cardId)
        {
            var result = await _lotteryService.GetTicketsByCardAsync(cardId);
            if (!result.Success)
                return BadRequest(new { message = result.Message });
            return Ok(result.Data);
        }

        [HttpGet]
        [ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> GetTicketCount([FromQuery] int cardId)
        {
            var result = await _lotteryService.GetTicketCountByCardAsync(cardId);
            if (!result.Success)
                return BadRequest(new { message = result.Message });
            return Ok(result.Data);
        }

        [HttpGet]
        [ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> GetTicketsByProgram([FromQuery] int programId)
        {
            var tenantId = _tenantProvider.CurrentTenantId ?? 0;
            var result = await _lotteryService.GetTicketsByProgramAsync(tenantId, programId);
            if (!result.Success)
                return BadRequest(new { message = result.Message });
            return Ok(result.Data);
        }

        [HttpGet]
        [ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> GetDraws([FromQuery] int programId)
        {
            var tenantId = _tenantProvider.CurrentTenantId ?? 0;
            var result = await _lotteryService.GetDrawsAsync(tenantId, programId);
            if (!result.Success)
                return BadRequest(new { message = result.Message });
            return Ok(result.Data);
        }

        [HttpPost]
        [ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> CreateDraw([FromBody] CreateLotteryDrawDto dto)
        {
            var tenantId = _tenantProvider.CurrentTenantId ?? 0;
            var result = await _lotteryService.CreateDrawAsync(dto, tenantId);
            if (!result.Success)
                return BadRequest(new { message = result.Message });
            return Ok(result.Data);
        }

        [HttpPost]
        [ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> ExecuteDraw([FromQuery] int drawId)
        {
            var result = await _lotteryService.ExecuteDrawAsync(drawId);
            if (!result.Success)
                return BadRequest(new { message = result.Message });
            return Ok(result.Data);
        }

        [HttpGet]
        [ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> GetWins([FromQuery] int cardId)
        {
            var result = await _lotteryService.GetWinsByCardAsync(cardId);
            if (!result.Success)
                return BadRequest(new { message = result.Message });
            return Ok(result.Data);
        }
    }
}

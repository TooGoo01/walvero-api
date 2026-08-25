using Loyalty.Business.Services.Abstractions.Main;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Loyalty.WebAPI.Controllers
{
    /// <summary>Mobil app (customer) üçün lotereya endpoint-ləri. Admin LotteryController-dən ayrı.</summary>
    [Route("api/lottery/[action]")]
    [ApiController]
    [Authorize(
        AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme,
        Policy = "CustomerOnly")]
    public class LotteryMobileController : ControllerBase
    {
        private readonly ILotteryService _lotteryService;
        private readonly ITenantProvider _tenantProvider;

        public LotteryMobileController(ILotteryService lotteryService, ITenantProvider tenantProvider)
        {
            _lotteryService = lotteryService;
            _tenantProvider = tenantProvider;
        }

        /// <summary>İstifadəçinin lotereya məlumatı: növbəti çəkiliş, bilet sayı, uduşlar.</summary>
        [HttpGet]
        public async Task<IActionResult> MyInfo([FromQuery] int tenantId, [FromQuery] int programId, [FromQuery] int cardId)
        {
            if (tenantId <= 0 || programId <= 0 || cardId <= 0)
                return BadRequest(new { message = "tenantId, programId və cardId tələb olunur" });

            var result = await _lotteryService.GetMyLotteryInfoAsync(tenantId, programId, cardId);
            if (!result.Success)
                return BadRequest(new { message = result.Message });
            return Ok(result.Data);
        }
    }
}

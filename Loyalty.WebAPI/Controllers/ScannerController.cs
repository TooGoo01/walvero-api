using Loyalty.Business.Dtos.Get.Main;
using Loyalty.Business.Dtos.Post.Main;
using Loyalty.Business.Results;
using Loyalty.Business.Services.Abstractions.Main;
using LoyaltyWalletApp.WEBUI.Tenancy;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Net;
using System.Security.Claims;

namespace Loyalty.WebAPI.Controllers
{
    [Route("api/scanner")]
    [ApiController]
    [Authorize(
        AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme,
        Policy = "TenantAdminOrModerator")]
    public class ScannerController : ControllerBase
    {
        private readonly IPosSessionService _posSessionService;
        private readonly ITenantProvider _tenantProvider;
        private readonly ILogger<ScannerController> _logger;

        public ScannerController(
            IPosSessionService posSessionService,
            ITenantProvider tenantProvider,
            ILogger<ScannerController> logger)
        {
            _posSessionService = posSessionService;
            _tenantProvider = tenantProvider;
            _logger = logger;
        }

        /// <summary>
        /// Start a scanner session
        /// </summary>
        [HttpPost("session/start")]
        [ProducesResponseType(typeof(ServiceResult<ScannerSessionResponseDto>), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> StartSession([FromBody] StartScannerSessionDto dto, CancellationToken ct)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            _logger.LogInformation("Starting scanner session for TenantId: {TenantId}, UserId: {UserId}, DeviceId: {DeviceId}, Barcode: {Barcode}",
                dto.UserId, dto.ScannerDeviceId, dto.Barcode);

            var result = await _posSessionService.StartSessionAsync(dto, ct);

            if (result.Success)
            {
                return Ok(result);
            }

            return BadRequest(result);
        }
    }
}


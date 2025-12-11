using Google.Apis.Auth.OAuth2;
using Loyalty.Business.Dtos.Post.Main;
using Loyalty.Business.Results;
using Loyalty.Business.Services.Abstractions.Main;
using Microsoft.AspNetCore.Mvc;
using System.Net;

namespace Loyalty.WebAPI.Controllers
{
    [Route("api/[controller]/[action]")]
    [ApiController]
    public class DeviceTokenController : ControllerBase
    {
        private readonly IDeviceTokenService _deviceTokenService;

        public DeviceTokenController(IDeviceTokenService deviceTokenService)
        {
            _deviceTokenService = deviceTokenService;
        }

        [HttpPost]
        [ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> Register([FromBody] RegisterDeviceTokenRequest request)
        {
            if (
                string.IsNullOrWhiteSpace(request.Token) ||
                string.IsNullOrWhiteSpace(request.Platform))
            {
                return BadRequest("UserId, Token, Platform are required.");
            }

            await 
                _deviceTokenService.RegisterAsync(request);
            return Ok(new { success = true });
        }

        /// <summary>
        /// Removes a device token (logout from device).
        /// Flutter calls this on logout.
        /// </summary>
        [HttpPost]
        [ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> Remove([FromBody] RemoveDeviceTokenRequest request)
        {
            if (request.UserId==0  ||
                string.IsNullOrWhiteSpace(request.Token))
            {
                return BadRequest("UserId and Token are required.");
            }

            await _deviceTokenService.RemoveAsync(request);
            return Ok(new { success = true });
        }

        /// <summary>
        /// Lists all device tokens belonging to a specific user.
        /// Useful for debugging or admin dashboard.
        /// </summary>
        //[HttpGet("user/{userId}")]
        //public async Task<IActionResult> GetUserDevices(string userId)
        //{
        //    var devices = await _manager.GetUserDevicesAsync(userId);
        //    return Ok(devices);
        //}

        /// <summary>
        /// Removes all tokens for a specific user.
        /// Very useful: “Logout from all devices”
        /// </summary>
        //[HttpPost]
        //[ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.OK)]
        //public async Task<IActionResult> ClearAll([FromBody] RemoveAllTokensRequest request)
        //{
        //    if (string.IsNullOrWhiteSpace(request.UserId))
        //        return BadRequest("UserId is required.");

        //    await _manager.RemoveAllAsync(request.UserId);
        //    return Ok(new { success = true });
        //}
    }
}

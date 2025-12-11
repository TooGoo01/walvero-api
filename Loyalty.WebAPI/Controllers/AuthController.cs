using Loyalty.Business.Dtos.Post.Users;
using Loyalty.Business.Results;
using Loyalty.Business.Services.Abstractions.Users;
using Microsoft.AspNetCore.Mvc;
using System.Net;

namespace Loyalty.WebAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly IGuestAuthService _guestAuthService;

        public AuthController(IGuestAuthService guestAuthService)
        {
            _guestAuthService = guestAuthService;
        }

        /// <summary>
        /// Guest login endpoint - creates or reuses guest user based on device ID
        /// </summary>
        [HttpPost("guest-login")]
        [ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> GuestLogin([FromBody] GuestLoginDto dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var result = await _guestAuthService.GuestLoginAsync(dto);
            
            if (result.Success)
            {
                return Ok(result);
            }

            return BadRequest(result.Message);
        }

        /// <summary>
        /// Refresh token endpoint - generates new access token using refresh token
        /// </summary>
        [HttpPost("refresh-token")]
        [ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenDto dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var result = await _guestAuthService.RefreshTokenAsync(dto);
            
            if (result.Success)
            {
                return Ok(result);
            }

            return BadRequest(result.Message);
        }
    }
}


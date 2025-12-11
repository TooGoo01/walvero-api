using Loyalty.Business.Dtos.Post.Users;
using Loyalty.Business.Results;
using Loyalty.Business.Services.Abstractions.Users;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Net;

namespace Loyalty.WebAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(
        AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme,
        Policy = "CustomerOnly")]
    public class ProfileController : ControllerBase
    {
        private readonly IGuestAuthService _guestAuthService;

        public ProfileController(IGuestAuthService guestAuthService)
        {
            _guestAuthService = guestAuthService;
        }

        /// <summary>
        /// Complete profile - upgrade guest user to full user
        /// </summary>
        [HttpPost("complete")]
        [ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> CompleteProfile([FromBody] CompleteProfileDto dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var result = await _guestAuthService.CompleteProfileAsync(dto);
            
            if (result.Success)
            {
                return Ok(result.Data);
            }

            return BadRequest(result.Message);
        }

        /// <summary>
        /// Get profile completion status
        /// </summary>
        [HttpGet("status")]
        [ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> GetProfileStatus()
        {
            var result = await _guestAuthService.GetProfileStatusAsync();
            
            if (result.Success)
            {
                return Ok(result.Data);
            }

            return BadRequest(result.Message);
        }
    }
}


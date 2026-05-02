using Loyalty.Business.Dtos.Get.Users;
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
        private readonly IUserService _userService;

        public ProfileController(IGuestAuthService guestAuthService, IUserService userService)
        {
            _guestAuthService = guestAuthService;
            _userService = userService;
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
        /// JWT-dən cari user-i fetch edir və tam profil məlumatlarını qaytarır.
        /// Frontend profile screen açılarkən cache yerinə server-dən təzə user data
        /// almaq üçün istifadə edir (məs. başqa cihazdan dateOfBirth dəyişdirilibsə).
        /// </summary>
        [HttpGet("me")]
        [ProducesResponseType(typeof(ServiceResult<ApplicationUserDto>), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> GetMyProfile()
        {
            var result = await _userService.GetMyProfileAsync();
            if (!result.Success) return BadRequest(result);
            return Ok(result);
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

        /// <summary>
        /// Update user profile information
        /// </summary>
        [HttpPost("update")]
        [ProducesResponseType(typeof(ServiceResult<UpdateProfileResponseDto>), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileDto dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var result = await _userService.UpdateProfileAsync(dto);
            
            if (result.Success)
            {
                return Ok(result);
            }

            return BadRequest(result);
        }

        /// <summary>
        /// Verify old phone number for profile update
        /// </summary>
        [HttpPost("verify-old-phone")]
        [ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> VerifyOldPhone([FromBody] VerifyOldPhoneDto dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var result = await _userService.VerifyOldPhoneForUpdateAsync(dto.OtpCode);
            
            if (result.Success)
            {
                return Ok(result);
            }

            return BadRequest(result);
        }

        /// <summary>
        /// Verify new phone number and complete profile update
        /// </summary>
        [HttpPost("verify-new-phone")]
        [ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> VerifyNewPhone([FromBody] VerifyNewPhoneDto dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var result = await _userService.VerifyNewPhoneForUpdateAsync(dto.NewPhoneNumber, dto.OtpCode);
            
            if (result.Success)
            {
                return Ok(result);
            }

            return BadRequest(result);
        }

        /// <summary>
        /// Resend OTP for phone number update (old phone or new phone)
        /// </summary>
        [HttpPost("resend-phone-update-otp")]
        [ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> ResendPhoneUpdateOtp([FromBody] ResendPhoneUpdateOtpDto dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var result = await _userService.ResendPhoneUpdateOtpAsync(dto);
            
            if (result.Success)
            {
                return Ok(result);
            }

            return BadRequest(result);
        }
    }
}


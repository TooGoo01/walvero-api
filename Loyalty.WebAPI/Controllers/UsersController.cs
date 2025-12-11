
using Loyalty.Business.Dtos.Post.Users;
using Loyalty.Business.Results;
using Loyalty.Business.Services.Abstractions.Users;
using Microsoft.AspNetCore.Mvc;
using System.Net;

namespace Loyalty.WebAPI.Controllers
{
    [Route("api/[controller]/[action]")]
    [ApiController]
    public class UsersController : ControllerBase
    {
        private readonly IUserService _userService;

        public UsersController(IUserService userService)
        {
            _userService = userService;
        }

        [HttpGet]
        [ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> GetUser([FromBody] string userId)
        {
            return Ok(await _userService.GetUser(userId));
        }

        [HttpPost]
        [ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.OK)]
        public async Task<ActionResult<ServiceResult>> LogIn(LoginUserDto loginUser)
        {
            return Ok(await _userService.LogIn(loginUser));
        }

        [HttpPost]
        [ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.OK)]
        public async Task<ActionResult<ServiceResult>> LogOut()
        {
            return Ok(await _userService.LogOut());
        }

        [HttpPost]
        [ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.OK)]
        public async Task<ActionResult<ServiceResult>> Register([FromBody] RegisterUserDto registerUser)
        {
            var result = await _userService.Register(registerUser);
            if (result.Success)
            {
                
               
                    return Ok(result);
               

            }
            return new ServiceResult(false,result.Message);
        }

        [HttpGet]
        [ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.OK)]

        public async Task<ActionResult<ServiceResult>> GetUsers()
        {
            return Ok(await _userService.GetUsers());
        }

        [HttpPost]
        [ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.OK)]


        public async Task<ActionResult<ServiceResult>> AddClaim(AddClaimDto addClaim)
        {
            return Ok(await _userService.AddClaim(addClaim));
        }

        [HttpGet]
        [ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.OK)]
        public IActionResult GetLoggedUser()
        {
            return Ok(_userService.GetLoggedUser());
        }
        [HttpPost("password-reset")]
        [ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> PasswordReset(string email)
        {
            await _userService.PasswordResetAsync(email);
            return Ok();
        }
        [HttpPost("update-password")]
        [ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> UpdatePassword(string userName, string token, string password, string confirmPassword)
        {
            var result = await _userService.UpdatePassword(userName, token, password);
            if (result)
            {
                return Ok(result);
            }
            else
            {

            }
            return BadRequest(result);
        }
        [HttpPost("password-reset-verify")]
        [ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> PasswordResetVerify(string resetToken, string userName)
        {
            var response = await _userService.VerifyResetTokenAsync(resetToken, userName);
            return Ok(response);
        }


        [HttpPost]
        [ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> ConfirmEmail(string userName, string confirmToken)
        {
            var response = await _userService.VerifyConfirmAsync(userName, confirmToken);
            return Ok(response);
        }

        //[CustomAuthorize("SuperAdmin", "Owner")]
        [HttpPost]
        [ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.OK)]
        public async Task<ActionResult<ServiceResult>> AddUser(int userId)
        {
            var user = _userService.GetLoggedUser();
            await _userService.AddClaim(new AddClaimDto() { UserId = userId.ToString(), ClaimName = "Moderator", ClaimType = "Moderator" });
            return Ok();
        }

        // ========== PHONE-BASED OTP REGISTRATION ENDPOINTS ==========

        /// <summary>
        /// Register a new user with phone number (creates user in pending state and sends OTP)
        /// </summary>
        [HttpPost("register-with-phone")]
        [ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.OK)]
        public async Task<ActionResult<ServiceResult>> RegisterWithPhone([FromBody] RegisterWithPhoneDto registerDto)
        {
            var result = await _userService.RegisterUserWithPendingStatusAsync(registerDto);
            return Ok(result);
        }

        /// <summary>
        /// Verify OTP code for registration or password reset
        /// </summary>
        [HttpPost("verify-otp")]
        [ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.OK)]
        public async Task<ActionResult<ServiceResult>> VerifyOtp([FromBody] VerifyOtpDto verifyDto)
        {
            var purpose = verifyDto.Purpose == "PasswordReset" 
                ? Loyalty.DataAccess.Enums.OtpPurpose.PasswordReset 
                : Loyalty.DataAccess.Enums.OtpPurpose.Registration;
            
            var result = await _userService.VerifyOtpAsync(verifyDto.UserId, verifyDto.OtpCode, purpose);
            return Ok(result);
        }

        // ========== PHONE-BASED PASSWORD RESET ENDPOINTS ==========

        /// <summary>
        /// Request password reset OTP (sends OTP to phone number)
        /// </summary>
        [HttpPost("forgot-password")]
        [ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.OK)]
        public async Task<ActionResult<ServiceResult>> ForgotPassword([FromBody] ForgotPasswordDto forgotPasswordDto)
        {
            var result = await _userService.StartForgotPasswordAsync(forgotPasswordDto.PhoneNumber);
            return Ok(result);
        }

        /// <summary>
        /// Verify OTP for password reset (returns reset token)
        /// </summary>
        [HttpPost("verify-forgot-password-otp")]
        [ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.OK)]
        public async Task<ActionResult<ServiceResult>> VerifyForgotPasswordOtp([FromBody] VerifyForgotPasswordOtpDto verifyDto)
        {
            var result = await _userService.VerifyForgotPasswordOtpAsync(verifyDto.PhoneNumber, verifyDto.OtpCode);
            return Ok(result);
        }

        /// <summary>
        /// Reset password using the reset token from OTP verification
        /// </summary>
        [HttpPost("reset-password")]
        [ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.OK)]
        public async Task<ActionResult<ServiceResult>> ResetPassword([FromBody] ResetPasswordDto resetDto)
        {
            var result = await _userService.ResetPasswordAsync(resetDto.ResetToken, resetDto.NewPassword);
            return Ok(result);
        }
    }
}

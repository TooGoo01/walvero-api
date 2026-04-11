    using Loyalty.Business.Dtos.Post.Main;
using Loyalty.Business.Results;
using Loyalty.Business.Services.Abstractions.Main;
using Microsoft.AspNetCore.Mvc;
using System.Net;

namespace Loyalty.WebAPI.Controllers
{
    /// <summary>
    /// Public enrollment – auth tələb etmir. Webdəki Add.cshtml flow-u ilə eynidir.
    /// Form → Start (OTP göndərir) → Verify (wallet URL qaytarır)
    /// </summary>
    [Route("api/public/enrollment")]
    [ApiController]
    public class PublicEnrollmentController : ControllerBase
    {
        private readonly ICustomerEnrollmentService _enrollmentService;

        public PublicEnrollmentController(ICustomerEnrollmentService enrollmentService)
        {
            _enrollmentService = enrollmentService;
        }

        /// <summary>
        /// Step 1: Customer yaradır, OTP göndərir.
        /// Returns: customerId, maskedDestination, cardId, programId
        /// </summary>
        [HttpPost("start")]
        [ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.OK)]
        [ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.BadRequest)]
        public async Task<ActionResult<ServiceResult>> Start([FromBody] PublicEnrollmentStartDto model)
        {
            if (!ModelState.IsValid)
                return BadRequest(new ServiceResult(false, "Invalid request data"));

            try
            {
                var result = await _enrollmentService.StartPublicEnrollmentAsync(model);
                if (result.Success)
                    return Ok(result);
                return BadRequest(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ServiceResult(false, $"Server error: {ex.Message}"));
            }
        }

        /// <summary>
        /// Step 2: OTP təsdiqləyir, wallet URL-lərini qaytarır.
        /// Returns: googleSaveUrl, applePassUrl, cardId
        /// </summary>
        [HttpPost("verify")]
        [ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.OK)]
        [ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.BadRequest)]
        public async Task<ActionResult<ServiceResult>> Verify([FromBody] VerifyCustomerOtpDto model)
        {
            if (!ModelState.IsValid)
                return BadRequest(new ServiceResult(false, "Invalid request data"));

            if (model.CustomerId <= 0)
                return BadRequest(new ServiceResult(false, "CustomerId is required"));

            if (string.IsNullOrWhiteSpace(model.Code))
                return BadRequest(new ServiceResult(false, "OTP kodu tələb olunur"));

            try
            {
                var result = await _enrollmentService.VerifyEnrollmentAsync(model);
                if (result.Success)
                    return Ok(result);
                return BadRequest(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ServiceResult(false, $"Server error: {ex.Message}"));
            }
        }
    }
}

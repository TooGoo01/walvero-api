using Loyalty.Business.Dtos.Post.Main;
using Loyalty.Business.Dtos.Post.Users;
using Loyalty.Business.Results;
using Loyalty.Business.Services.Abstractions.Main;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Net;

namespace Loyalty.WebAPI.Controllers
{
    [Route("api/[controller]/[action]")]
    [ApiController]
    [Authorize(
    AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme,
    Policy = "CustomerOnly")]
    public class CustomerController : ControllerBase
    {
        private readonly ICustomerEnrollmentService _customerEnrollmentService;

        public CustomerController(ICustomerEnrollmentService customerEnrollmentService)
        {
            _customerEnrollmentService = customerEnrollmentService;
        }

        [HttpPost]
        [ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.OK)]
        [ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.BadRequest)]
        public async Task<ActionResult<ServiceResult>> Add([FromBody] AddCustomerWithCardDto model)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(new ServiceResult(false, "Invalid request data"));
            }

            if (model == null)
            {
                return BadRequest(new ServiceResult(false, "Request body is required"));
            }

            if (model.TenantId <= 0)
            {
                return BadRequest(new ServiceResult(false, "TenantId is required and must be greater than 0"));
            }

            if (model.LoyaltyProgramId <= 0)
            {
                return BadRequest(new ServiceResult(false, "LoyaltyProgramId is required and must be greater than 0"));
            }

            try
            {
                var result = await _customerEnrollmentService.StartEnrollmentAsync(model);
                if (result.Success)
                {
                    return Ok(new ServiceResult(true, result.Data, "Adding completed"));
                }
                return BadRequest(new ServiceResult(false, result.Message));
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ServiceResult(false, $"Server error: {ex.Message}"));
            }
        }
      
    }
}

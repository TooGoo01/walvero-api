using Loyalty.Business.Results;
using Loyalty.Business.Services.Abstractions.Main;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;
using System.Net;

namespace Loyalty.WebAPI.Controllers
{
    [Route("api/[controller]/[action]")]
    [ApiController]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Policy = "SuperAdminOnly")]
    public class AppSettingController : ControllerBase
    {
        private readonly IAppSettingService _appSettingService;

        public AppSettingController(IAppSettingService appSettingService)
        {
            _appSettingService = appSettingService;
        }

        [HttpGet]
        [ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.OK)]
        [ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.Unauthorized)]
        public async Task<IActionResult> List(CancellationToken ct)
        {
            return Ok(await _appSettingService.GetAllAsync(ct));
        }

        [HttpPut]
        [ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.OK)]
        [ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.BadRequest)]
        [ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.Unauthorized)]
        public async Task<IActionResult> Set([FromBody] SetAppSettingRequest request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var result = await _appSettingService.SetAsync(request.Key, request.Value);
            if (result.Success)
                return Ok(result);

            return BadRequest(result);
        }
    }

    public class SetAppSettingRequest
    {
        [Required]
        public string Key { get; set; } = default!;

        [Required]
        public string Value { get; set; } = default!;
    }
}

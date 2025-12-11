using Loyalty.Business.Dtos.Post.Main;
using Loyalty.Business.Results;
using Loyalty.Business.Services.Abstractions.Main;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Net;

namespace Loyalty.WebAPI.Controllers
{
    [Route("api/[controller]/[action]")]
    [ApiController]
    public class VersionController : ControllerBase
    {
        private readonly IAppVersionService _appVersion;

        public VersionController(IAppVersionService appVersion)
        {
            _appVersion = appVersion;
        }

        [HttpGet]
        [ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.OK)]

        public async Task<ActionResult<ServiceResult>> Check([FromQuery] VersionCheckRequest? model)
        {
            return Ok(await _appVersion.Check(model.Platform,model.CurrentVersion));
        }
    }
}

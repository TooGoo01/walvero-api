using Loyalty.Business.Dtos.Main;
using Loyalty.Business.Results;
using Loyalty.Business.Services.Abstractions.Main;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Loyalty.WebAPI.Controllers
{
    /// <summary>
    /// AI köməkçi üçün xarici məkan bilik bazası (app-da görünmür). Yalnız Super Admin.
    /// </summary>
    [Route("api/[controller]/[action]")]
    [ApiController]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Policy = "SuperAdminOnly")]
    public class AiReferencePlaceController : ControllerBase
    {
        private readonly IReferencePlaceService _service;

        public AiReferencePlaceController(IReferencePlaceService service)
        {
            _service = service;
        }

        [HttpGet]
        public async Task<IActionResult> List(CancellationToken ct)
            => Ok(await _service.GetAllAsync(ct));

        [HttpPost]
        public async Task<IActionResult> Add([FromBody] CreateAiReferencePlaceDto dto, CancellationToken ct)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            var r = await _service.AddAsync(dto, ct);
            return r.Success ? Ok(r) : BadRequest(r);
        }

        [HttpPost]
        public async Task<IActionResult> Update([FromBody] UpdateAiReferencePlaceDto dto, CancellationToken ct)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            var r = await _service.UpdateAsync(dto, ct);
            return r.Success ? Ok(r) : BadRequest(r);
        }

        [HttpPost]
        public async Task<IActionResult> Delete([FromQuery] int id, CancellationToken ct)
        {
            var r = await _service.DeleteAsync(id, ct);
            return r.Success ? Ok(r) : BadRequest(r);
        }
    }
}

using Loyalty.Business.Dtos.Main;
using Loyalty.Business.Results;
using Loyalty.Business.Services.Abstractions.Main;
using Loyalty.Business.Services.Abstractions.Storage;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Loyalty.WebAPI.Controllers
{
    /// <summary>Super admin: link seçimi səhifələri (/l/{slug}) — CRUD, şəkil yükləmə, klik statistikası.</summary>
    [Route("api/[controller]/[action]")]
    [ApiController]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Policy = "SuperAdminOnly")]
    public class LinkPageController : ControllerBase
    {
        private const long MaxImageBytes = 5 * 1024 * 1024;

        private readonly ILinkPageService _service;
        private readonly IStorage _storage;

        public LinkPageController(ILinkPageService service, IStorage storage)
        {
            _service = service;
            _storage = storage;
        }

        [HttpGet]
        public async Task<IActionResult> List(CancellationToken ct)
            => Ok(await _service.ListAsync(ct));

        [HttpGet]
        public async Task<IActionResult> Get([FromQuery] int id, CancellationToken ct)
        {
            var r = await _service.GetAsync(id, ct);
            return r.Success ? Ok(r) : NotFound(r);
        }

        [HttpPost]
        public async Task<IActionResult> Add([FromBody] CreateLinkPageDto dto, CancellationToken ct)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            var r = await _service.CreateAsync(dto, ct);
            return r.Success ? Ok(r) : BadRequest(r);
        }

        [HttpPost]
        public async Task<IActionResult> Update([FromBody] UpdateLinkPageDto dto, CancellationToken ct)
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

        [HttpGet]
        public async Task<IActionResult> Stats([FromQuery] int id, [FromQuery] DateTime? from, [FromQuery] DateTime? to, CancellationToken ct)
        {
            var r = await _service.GetStatsAsync(id, from, to, ct);
            return r.Success ? Ok(r) : NotFound(r);
        }

        /// <summary>Logo / fon şəkli → public URL.</summary>
        [HttpPost]
        public async Task<IActionResult> UploadImage()
        {
            var file = Request.Form.Files.FirstOrDefault();
            if (file == null || file.Length == 0)
                return BadRequest(new ServiceResult(false, "Fayl yoxdur"));
            if (file.Length > MaxImageBytes)
                return BadRequest(new ServiceResult(false, "Şəkil 5 MB-dan böyük ola bilməz"));

            var uploaded = await _storage.UploadFileAsync("link-pages", new[] { file });
            if (uploaded.Count == 0)
                return BadRequest(new ServiceResult(false, "Yükləmə alınmadı"));

            return Ok(new ServiceResult(true, new { url = uploaded[0].pathOrContainerName }));
        }
    }
}

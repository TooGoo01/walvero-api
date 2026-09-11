using Loyalty.Business.Dtos.Main;
using Loyalty.Business.Results;
using Loyalty.Business.Services.Abstractions.Main;
using Loyalty.Business.Services.Abstractions.Storage;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Net;

namespace Loyalty.WebAPI.Controllers
{
    /// <summary>Menu-app kampaniya popup-larının tenant admin idarəsi.</summary>
    [Route("api/[controller]/[action]")]
    [ApiController]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Policy = "TenantAdminOrModerator")]
    public class MenuPopupController : ControllerBase
    {
        private readonly IMenuPopupService _service;
        private readonly ITenantProvider _tenantProvider;
        private readonly IStorage _storage;

        public MenuPopupController(IMenuPopupService service, ITenantProvider tenantProvider, IStorage storage)
        {
            _service = service;
            _tenantProvider = tenantProvider;
            _storage = storage;
        }

        private int? TenantId => _tenantProvider.CurrentTenantId;

        [HttpGet]
        public async Task<IActionResult> List(CancellationToken ct)
        {
            if (TenantId is not > 0) return BadRequest(new ServiceResult(false, "TenantId tapılmadı"));
            return Ok(await _service.ListAsync(TenantId.Value, ct));
        }

        [HttpPost]
        public async Task<IActionResult> Save([FromBody] SaveMenuPopupDto dto, CancellationToken ct)
        {
            if (TenantId is not > 0) return BadRequest(new ServiceResult(false, "TenantId tapılmadı"));
            var r = await _service.SaveAsync(TenantId.Value, dto, ct);
            return r.Success ? Ok(r) : BadRequest(r);
        }

        [HttpPost]
        public async Task<IActionResult> Delete([FromQuery] int id, CancellationToken ct)
        {
            if (TenantId is not > 0) return BadRequest(new ServiceResult(false, "TenantId tapılmadı"));
            var r = await _service.DeleteAsync(TenantId.Value, id, ct);
            return r.Success ? Ok(r) : BadRequest(r);
        }

        /// <summary>Popup şəklini file kimi yüklə → public URL qaytar.</summary>
        [HttpPost]
        [ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> UploadImage()
        {
            var file = Request.Form.Files.FirstOrDefault();
            if (file == null || file.Length == 0)
                return BadRequest(new ServiceResult(false, "Fayl yoxdur"));
            if (file.Length > 5_000_000)
                return BadRequest(new ServiceResult(false, "Şəkil 5 MB-dan böyükdür"));

            var uploaded = await _storage.UploadFileAsync("menu-popups", new[] { file });
            if (uploaded.Count == 0)
                return BadRequest(new ServiceResult(false, "Yükləmə alınmadı"));

            var url = uploaded[0].pathOrContainerName;
            return Ok(new ServiceResult(true, new { url }));
        }
    }
}

using Loyalty.Business.Results;
using Loyalty.Business.Services.Abstractions.Main;
using Loyalty.Business.Services.Abstractions.Storage;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Net;

namespace Loyalty.WebAPI.Controllers
{
    [Route("api/[controller]/[action]")]
    [ApiController]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Policy = "SuperAdminOnly")]
    public class AppSettingController : ControllerBase
    {
        private readonly IAppSettingService _appSettingService;
        private readonly IStorage _storage;

        public AppSettingController(IAppSettingService appSettingService, IStorage storage)
        {
            _appSettingService = appSettingService;
            _storage = storage;
        }

        /// <summary>Qlobal app assetini (splash fon/logo) file kimi yüklə → public URL qaytar.</summary>
        [HttpPost]
        [ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.OK)]
        [ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.BadRequest)]
        public async Task<IActionResult> UploadImage()
        {
            var file = Request.Form.Files.FirstOrDefault();
            if (file == null || file.Length == 0)
                return BadRequest(new ServiceResult(false, "Fayl yoxdur"));

            var uploaded = await _storage.UploadFileAsync("app-assets", new[] { file });
            if (uploaded.Count == 0)
                return BadRequest(new ServiceResult(false, "Yükləmə alınmadı"));

            var url = uploaded[0].pathOrContainerName; // 2-ci element public URL-dir
            return Ok(new ServiceResult(true, new { url }));
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

        // AllowEmptyStrings: boş dəyər = açarı sıfırla (məs. splash şəklini sil)
        [Required(AllowEmptyStrings = true)]
        public string Value { get; set; } = default!;
    }
}

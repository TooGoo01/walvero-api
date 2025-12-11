using Loyalty.Business.Services.Abstractions.Main;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;
using System.Globalization;
using static Loyalty.Business.Services.Implementations.Main.PasskitService;

namespace Loyalty.WebAPI.Controllers
{
    [ApiController]
    [Route("v1")]
    public sealed class PasskitController : ControllerBase
    {
        private readonly IPasskitService _svc;
        private readonly ILogger<PasskitController> _logger;

        public PasskitController(IPasskitService svc, ILogger<PasskitController> logger)
        {
            _svc = svc;
            _logger = logger;
        }

        // POST /v1/devices/{deviceLibraryIdentifier}/registrations/{passTypeIdentifier}/{serialNumber}
        [HttpPost("devices/{deviceLibId}/registrations/{passTypeId}/{serial}")]
        public async Task<IActionResult> Register(
            string deviceLibId, string passTypeId, string serial,
            [FromBody] RegisterDto? body, CancellationToken ct)
        {
            var auth = Request.Headers["Authorization"].ToString();
            var ua = Request.Headers.UserAgent.ToString();
            var ip = HttpContext.Connection.RemoteIpAddress?.ToString();

            _logger.LogInformation("Apple Register request: device={DeviceLibId}, passType={PassTypeId}, serial={Serial}, UA={UA}, IP={IP}, AuthHeader={AuthHeader}, Body={Body}",
                deviceLibId, passTypeId, serial, ua, ip, auth, body is null ? "null" : System.Text.Json.JsonSerializer.Serialize(body));

            if (string.IsNullOrWhiteSpace(auth))
                return Unauthorized("Missing Authorization header.");

            if (body is null || string.IsNullOrWhiteSpace(body.PushToken))
                return BadRequest("pushToken is required.");

            var res = await _svc.RegisterAsync(deviceLibId, passTypeId, serial, auth, body.PushToken.Trim(), ua, ct);

            _logger.LogInformation("Register result: Success={Success}, Message={Message}", res.Success, res.Message);

            if (!res.Success)
                return Unauthorized(string.IsNullOrWhiteSpace(res.Message) ? "Unauthorized" : res.Message);

            Response.Headers["Cache-Control"] = "no-store";
            if (res.AlreadyExists == true)
                return Ok(); // 200

            return Created(string.Empty, null); // 201
        }

        public sealed class RegisterDto
        {
            public string PushToken { get; set; } = default!;
        }

        // DELETE /v1/devices/{deviceLibraryIdentifier}/registrations/{passTypeIdentifier}/{serialNumber}
        [HttpDelete("devices/{deviceLibId}/registrations/{passTypeId}/{serial}")]
        public async Task<IActionResult> Unregister(
            string deviceLibId, string passTypeId, string serial, CancellationToken ct)
        {
            var auth = Request.Headers["Authorization"].ToString();
            var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
            _logger.LogInformation("Apple Unregister: device={Device}, passType={Type}, serial={Serial}, IP={IP}, Auth={Auth}",
                deviceLibId, passTypeId, serial, ip, auth);

            var res = await _svc.UnregisterAsync(deviceLibId, passTypeId, serial, auth, ct);
            _logger.LogInformation("Unregister result: Success={Success}, Message={Message}", res.Success, res.Message);

            if (!res.Success)
            {
                if (string.Equals(res.Message, "Unauthorized", StringComparison.OrdinalIgnoreCase))
                    return Unauthorized();

                return NotFound(res.Message);
            }

            return Ok();
        }

        // GET /v1/passes/{passTypeIdentifier}/{serialNumber}
        [HttpGet("passes/{passTypeId}/{serial}")]
        public async Task<IActionResult> GetPass(string passTypeId, string serial, CancellationToken ct)
        {
            var ua = Request.Headers.UserAgent.ToString();
            var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
            _logger.LogInformation("Apple GetPass: passType={PassType}, serial={Serial}, UA={UA}, IP={IP}", passTypeId, serial, ua, ip);

            var metaRes = await _svc.GetPassMetaAsync(passTypeId, serial, ct);
            if (!metaRes.Success || metaRes.Data is not PassMeta meta || !meta.Exists)
            {
                _logger.LogWarning("GetPass meta not found for serial={Serial}", serial);
                return NotFound(string.IsNullOrWhiteSpace(metaRes.Message) ? "Not found" : metaRes.Message);
            }

            var ifNoneMatch = Request.Headers[HeaderNames.IfNoneMatch].ToString();
            var ifModifiedSince = Request.Headers[HeaderNames.IfModifiedSince].ToString();
            _logger.LogDebug("Headers: If-None-Match={IfNoneMatch}, If-Modified-Since={IfModifiedSince}",
                ifNoneMatch, ifModifiedSince);

            var clientHasFreshCopy =
                (!string.IsNullOrEmpty(ifNoneMatch) && string.Equals(ifNoneMatch.Trim(), meta.ETag, StringComparison.Ordinal)) ||
                (DateTimeOffset.TryParse(ifModifiedSince, out var ims) && meta.LastModified <= ims);

            if (clientHasFreshCopy)
            {
                _logger.LogInformation("Apple GetPass: 304 Not Modified for serial={Serial}", serial);
                return StatusCode(304);
            }

            var passPathRes = await _svc.GetPassPathAsync(passTypeId, serial, ct);
            if (!passPathRes.Success || passPathRes.Data is not string pkpassPath)
            {
                _logger.LogWarning("Apple GetPass: pkpass file not found for serial={Serial}", serial);
                return NotFound(string.IsNullOrWhiteSpace(passPathRes.Message) ? "Pass file not found" : passPathRes.Message);
            }

            _logger.LogInformation("Apple GetPass: returning pkpass {Path}", pkpassPath);

            Response.Headers["Cache-Control"] = "no-store";
            if (!string.IsNullOrEmpty(meta.ETag))
                Response.Headers[HeaderNames.ETag] = meta.ETag;

            Response.Headers[HeaderNames.LastModified] = meta.LastModified.ToUniversalTime().ToString("R", CultureInfo.InvariantCulture);

            return PhysicalFile(pkpassPath, "application/vnd.apple.pkpass", enableRangeProcessing: true);
        }

        // GET /v1/devices/{deviceLibraryIdentifier}/registrations/{passTypeIdentifier}?passesUpdatedSince=<tag>
        [HttpGet("devices/{deviceLibId}/registrations/{passTypeId}")]
        public async Task<IActionResult> ListUpdated(
      string deviceLibId,
      string passTypeId,
      [FromQuery(Name = "passesUpdatedSince")] string? passesUpdatedSince,
      CancellationToken ct)
        {
            _logger.LogInformation("Apple ListUpdated: device={Device}, passType={Type}, since={Since}",
                deviceLibId, passTypeId, passesUpdatedSince ?? "<null>");

            var res = await _svc.ListUpdatedAsync(deviceLibId, passTypeId, passesUpdatedSince, ct);

            if (!res.Success)
                return BadRequest(string.IsNullOrWhiteSpace(res.Message) ? "Bad request" : res.Message);

            if (res.Data is UpdatedListDto dto)
            {
                if (dto.Serials is null || dto.Serials.Count == 0)
                {
                    _logger.LogInformation("ListUpdated → 204 NoContent (no serials)");
                    return NoContent();
                }

                // Apple üçün ISO-8601 UTC formatı
                string lastUpdatedIso;
                try
                {
                    // `lastUpdatedTag`-i (UTC formatında) ISO 8601-ə çeviririk
                    lastUpdatedIso = DateTime.Parse(dto.LastUpdatedTag).ToString("o"); // ISO 8601 formatı
                }
                catch (Exception ex)
                {
                    _logger.LogError("Timestamp çevrilməsi zamanı səhv baş verdi: {ErrorMessage}", ex.Message);
                    lastUpdatedIso = dto.LastUpdatedTag; // Əgər xəta baş verərsə, fallback olaraq `LastUpdatedTag`-ı istifadə edirik
                }

                _logger.LogInformation("ListUpdated → 200 OK, count={Count}, serials={Serials}, lastUpdated={Last}",
                    dto.Serials.Count, string.Join(",", dto.Serials), lastUpdatedIso);

                return Ok(new
                {
                    serialNumbers = dto.Serials,
                    lastUpdated = lastUpdatedIso
                });
            }

            if (res.Data is not null)
            {
                _logger.LogInformation("ListUpdated → 200 OK (preformatted), data={Type}", res.Data.GetType().Name);
                return Ok(res.Data);
            }

            _logger.LogInformation("ListUpdated → 204 NoContent (no data)");
            return NoContent();
        }



        // (Opsional) POST /v1/log — iOS Wallet diaqnostika logları üçün
        [HttpPost("log")]
        [AllowAnonymous]
        public IActionResult Log([FromBody] object? payload)
        {
            var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
            _logger.LogInformation("Apple /v1/log payload from {IP}: {Payload}", ip, payload);
            return Ok();
        }
    }
}

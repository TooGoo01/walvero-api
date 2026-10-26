using Loyalty.Business.Dtos.Post.Main;
using Loyalty.Business.Results;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Loyalty.WebAPI.Controllers
{
    /// <summary>
    /// walvero.com error boundary-lərindən gələn brauzer xətaları. Next.js "Application error" ekranı
    /// serverə heç nə göndərmir və walvero-next App Insights yalnız server request-lərini yazır —
    /// bu endpoint xətanı API log-una (Serilog → App Insights traces, "[CLIENT-ERROR]") salır.
    /// </summary>
    [Route("api/public/client-log")]
    [ApiController]
    [AllowAnonymous]
    public class ClientLogController : ControllerBase
    {
        private readonly ILogger<ClientLogController> _logger;

        public ClientLogController(ILogger<ClientLogController> logger)
        {
            _logger = logger;
        }

        [HttpPost("error")]
        public IActionResult Error([FromBody] ClientErrorDto? dto)
        {
            // Fire-and-forget: xəta ekranı ikinci xəta yaratmasın — həmişə 200.
            try
            {
                if (dto is not null)
                {
                    _logger.LogWarning(
                        "[CLIENT-ERROR] source={Source} url={Url} locale={Locale} digest={Digest} ua={UserAgent} message={Message} stack={Stack}",
                        Cut(dto.Source, 100),
                        Cut(dto.Url, 500),
                        Cut(dto.Locale, 100),
                        Cut(dto.Digest, 100),
                        Cut(dto.UserAgent, 500),
                        Cut(dto.Message, 1000),
                        Cut(dto.Stack, 4000));
                }
            }
            catch
            {
                // ignore — loglama kritik deyil
            }
            return Ok(new ServiceResult(true));
        }

        // Anonim endpoint — ingestion/abuse qoruması üçün sahələri kəsirik.
        private static string Cut(string? value, int max)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            return value.Length <= max ? value : value[..max];
        }
    }
}

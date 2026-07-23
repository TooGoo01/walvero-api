using Loyalty.Business.Results;
using Loyalty.Business.Services.Abstractions.Main;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;
using System.Net;

namespace Loyalty.WebAPI.Controllers
{
    // Telegram botunun qlobal konfiqurasiyası — yalnız super admin panel üçün
    [Route("api/telegram/admin")]
    [ApiController]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Policy = "SuperAdminOnly")]
    public class TelegramAdminController : ControllerBase
    {
        private readonly ITelegramSettingsProvider _settingsProvider;
        private readonly IAppSettingService _appSettingService;
        private readonly ITelegramSender _sender;

        public TelegramAdminController(
            ITelegramSettingsProvider settingsProvider,
            IAppSettingService appSettingService,
            ITelegramSender sender)
        {
            _settingsProvider = settingsProvider;
            _appSettingService = appSettingService;
            _sender = sender;
        }

        [HttpGet("settings")]
        [ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> GetSettings(CancellationToken ct)
        {
            var settings = await _settingsProvider.GetAsync(ct);
            var hasToken = !string.IsNullOrEmpty(settings.BotToken);

            var me = hasToken ? await _sender.GetMeAsync(ct) : null;
            var webhook = hasToken ? await _sender.GetWebhookInfoAsync(ct) : null;

            return Ok(new ServiceResult(true, new
            {
                botUsername = settings.BotUsername,
                hasToken,
                tokenMask = hasToken ? "…" + settings.BotToken[^Math.Min(4, settings.BotToken.Length)..] : null,
                hasWebhookSecret = !string.IsNullOrEmpty(settings.WebhookSecret),
                botOk = me?.Ok == true,
                botInfo = me?.Result == null ? null : new { me.Result.Username, me.Result.FirstName },
                botError = me != null && !me.Ok ? me.Description : null,
                webhook = webhook?.Result == null ? null : new
                {
                    url = webhook.Result.Url,
                    pendingUpdateCount = webhook.Result.PendingUpdateCount,
                    lastErrorMessage = webhook.Result.LastErrorMessage,
                    lastErrorDate = webhook.Result.LastErrorDate == null
                        ? (DateTime?)null
                        : DateTimeOffset.FromUnixTimeSeconds(webhook.Result.LastErrorDate.Value).UtcDateTime
                }
            }));
        }

        [HttpPut("settings")]
        [ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.OK)]
        [ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.BadRequest)]
        public async Task<IActionResult> UpdateSettings([FromBody] UpdateTelegramSettingsRequest request)
        {
            var updated = 0;

            if (!string.IsNullOrWhiteSpace(request.BotToken))
            {
                await _appSettingService.SetAsync(TelegramSettingKeys.BotToken, request.BotToken.Trim());
                updated++;
            }

            if (!string.IsNullOrWhiteSpace(request.BotUsername))
            {
                await _appSettingService.SetAsync(TelegramSettingKeys.BotUsername,
                    request.BotUsername.Trim().TrimStart('@'));
                updated++;
            }

            if (!string.IsNullOrWhiteSpace(request.WebhookSecret))
            {
                await _appSettingService.SetAsync(TelegramSettingKeys.WebhookSecret, request.WebhookSecret.Trim());
                updated++;
            }

            if (updated == 0)
                return BadRequest(new ServiceResult(false, "Dəyişdiriləcək sahə göndərilməyib"));

            _settingsProvider.Invalidate();
            return Ok(new ServiceResult(true, new { updated }, "Telegram tənzimləmələri yeniləndi"));
        }

        [HttpPost("set-webhook")]
        [ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.OK)]
        [ProducesResponseType(typeof(ServiceResult), (int)HttpStatusCode.BadRequest)]
        public async Task<IActionResult> SetWebhook([FromBody] SetTelegramWebhookRequest request, CancellationToken ct)
        {
            if (!request.BaseUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                return BadRequest(new ServiceResult(false, "Base URL https:// ilə başlamalıdır"));

            var settings = await _settingsProvider.GetAsync(ct);
            if (string.IsNullOrEmpty(settings.BotToken))
                return BadRequest(new ServiceResult(false, "Əvvəlcə bot token təyin edin"));
            if (string.IsNullOrEmpty(settings.WebhookSecret))
                return BadRequest(new ServiceResult(false, "Əvvəlcə webhook secret təyin edin"));

            var url = request.BaseUrl.TrimEnd('/') + "/api/telegram/webhook";
            var result = await _sender.SetWebhookAsync(url, settings.WebhookSecret, ct);

            if (result == null)
                return BadRequest(new ServiceResult(false, "Telegram API-yə çatmaq mümkün olmadı"));
            if (!result.Ok)
                return BadRequest(new ServiceResult(false, $"Telegram xətası: {result.Description}"));

            return Ok(new ServiceResult(true, new { url }, "Webhook quruldu"));
        }
    }

    public class UpdateTelegramSettingsRequest
    {
        public string? BotToken { get; set; }
        public string? BotUsername { get; set; }
        public string? WebhookSecret { get; set; }
    }

    public class SetTelegramWebhookRequest
    {
        [Required]
        public string BaseUrl { get; set; } = default!;
    }
}

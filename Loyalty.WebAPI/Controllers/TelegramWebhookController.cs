using System.Security.Cryptography;
using System.Text;
using Loyalty.Business.Dtos.Telegram;
using Loyalty.Business.Services.Abstractions.Main;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Loyalty.WebAPI.Controllers
{
    [ApiController]
    [Route("api/telegram")]
    public class TelegramWebhookController : ControllerBase
    {
        private const string SecretHeaderName = "X-Telegram-Bot-Api-Secret-Token";

        private readonly ITelegramBotService _botService;
        private readonly ITelegramSettingsProvider _settingsProvider;
        private readonly ILogger<TelegramWebhookController> _logger;

        public TelegramWebhookController(
            ITelegramBotService botService,
            ITelegramSettingsProvider settingsProvider,
            ILogger<TelegramWebhookController> logger)
        {
            _botService = botService;
            _settingsProvider = settingsProvider;
            _logger = logger;
        }

        [HttpPost("webhook")]
        [AllowAnonymous]
        public async Task<IActionResult> Webhook([FromBody] TelegramUpdateDto update, CancellationToken ct)
        {
            var expectedSecret = (await _settingsProvider.GetAsync(ct)).WebhookSecret;
            var providedSecret = Request.Headers[SecretHeaderName].FirstOrDefault();

            if (string.IsNullOrEmpty(expectedSecret) || !FixedTimeEquals(expectedSecret, providedSecret))
                return Unauthorized();

            try
            {
                await _botService.HandleUpdateAsync(update, ct);
            }
            catch (Exception ex)
            {
                // Həmişə 200 — Telegram non-200-ləri aqressiv retry edir, poison update loop olmasın
                _logger.LogError(ex, "Telegram update emalı xəta verdi. UpdateId: {UpdateId}", update?.UpdateId);
            }

            return Ok();
        }

        private static bool FixedTimeEquals(string expected, string? provided)
        {
            if (provided == null)
                return false;

            var expectedBytes = Encoding.UTF8.GetBytes(expected);
            var providedBytes = Encoding.UTF8.GetBytes(provided);
            return CryptographicOperations.FixedTimeEquals(expectedBytes, providedBytes);
        }
    }
}

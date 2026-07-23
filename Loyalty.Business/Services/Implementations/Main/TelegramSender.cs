using System.Text;
using System.Text.Json;
using Loyalty.Business.Dtos.Telegram;
using Loyalty.Business.Services.Abstractions.Main;
using Microsoft.Extensions.Logging;

namespace Loyalty.Business.Services.Implementations.Main
{
    public class TelegramSender : ITelegramSender
    {
        public const string HttpClientName = "TelegramBot";

        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ITelegramSettingsProvider _settingsProvider;
        private readonly ILogger<TelegramSender> _logger;

        public TelegramSender(
            IHttpClientFactory httpClientFactory,
            ITelegramSettingsProvider settingsProvider,
            ILogger<TelegramSender> logger)
        {
            _httpClientFactory = httpClientFactory;
            _settingsProvider = settingsProvider;
            _logger = logger;
        }

        public Task SendMessageAsync(long chatId, string text, object? replyMarkup = null, CancellationToken ct = default)
        {
            var payload = new Dictionary<string, object?>
            {
                ["chat_id"] = chatId,
                ["text"] = text,
                ["parse_mode"] = "HTML"
            };
            if (replyMarkup != null)
                payload["reply_markup"] = replyMarkup;

            return PostFireAndForgetAsync("sendMessage", payload, ct);
        }

        public Task AnswerCallbackQueryAsync(string callbackQueryId, CancellationToken ct = default)
            => PostFireAndForgetAsync("answerCallbackQuery", new Dictionary<string, object?>
            {
                ["callback_query_id"] = callbackQueryId
            }, ct);

        public Task<TelegramApiResponse<TelegramBotInfoDto>?> GetMeAsync(CancellationToken ct = default)
            => CallAsync<TelegramBotInfoDto>("getMe", null, ct);

        public Task<TelegramApiResponse<bool>?> SetWebhookAsync(string url, string secret, CancellationToken ct = default)
            => CallAsync<bool>("setWebhook", new Dictionary<string, object?>
            {
                ["url"] = url,
                ["secret_token"] = secret,
                ["allowed_updates"] = new[] { "message", "callback_query" }
            }, ct);

        public Task<TelegramApiResponse<TelegramWebhookInfoDto>?> GetWebhookInfoAsync(CancellationToken ct = default)
            => CallAsync<TelegramWebhookInfoDto>("getWebhookInfo", null, ct);

        // Xəta webhook cavabını pozmasın deyə log + udmaq
        private async Task PostFireAndForgetAsync(string method, object payload, CancellationToken ct)
        {
            try
            {
                await SendRawAsync(method, payload, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Telegram {Method} çağırışı xəta verdi", method);
            }
        }

        // Admin əməliyyatları üçün — cavab zərfi qaytarılır, şəbəkə xətası null
        private async Task<TelegramApiResponse<T>?> CallAsync<T>(string method, object? payload, CancellationToken ct)
        {
            try
            {
                var response = await SendRawAsync(method, payload, ct);
                if (response == null)
                    return null;

                var body = await response.Content.ReadAsStringAsync(ct);
                return JsonSerializer.Deserialize<TelegramApiResponse<T>>(body);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Telegram {Method} çağırışı xəta verdi", method);
                return null;
            }
        }

        private async Task<HttpResponseMessage?> SendRawAsync(string method, object? payload, CancellationToken ct)
        {
            var settings = await _settingsProvider.GetAsync(ct);
            if (string.IsNullOrEmpty(settings.BotToken))
            {
                _logger.LogWarning("Telegram:BotToken konfiqurasiya olunmayıb — {Method} göndərilmədi", method);
                return null;
            }

            var client = _httpClientFactory.CreateClient(HttpClientName);
            HttpResponseMessage response;

            // Diqqət: token-də ":" var — "bot{token}/..." prefiksiz yazılsa .NET onu
            // absolut URI scheme kimi parse edir (NotSupportedException). "/" məcburidir.
            var requestUri = $"/bot{settings.BotToken}/{method}";

            if (payload == null)
            {
                response = await client.GetAsync(requestUri, ct);
            }
            else
            {
                var json = JsonSerializer.Serialize(payload);
                using var content = new StringContent(json, Encoding.UTF8, "application/json");
                response = await client.PostAsync(requestUri, content, ct);
            }

            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(ct);
                _logger.LogError("Telegram {Method} uğursuz: {Status} {Body}", method, (int)response.StatusCode, body);
            }

            return response;
        }
    }
}

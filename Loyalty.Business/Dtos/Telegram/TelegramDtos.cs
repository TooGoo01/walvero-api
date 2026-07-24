using System.Text.Json.Serialization;

namespace Loyalty.Business.Dtos.Telegram
{
    // Telegram Bot API update payload — yalnız istifadə olunan sahələr
    public class TelegramUpdateDto
    {
        [JsonPropertyName("update_id")]
        public long UpdateId { get; set; }

        [JsonPropertyName("message")]
        public TelegramMessageDto? Message { get; set; }

        [JsonPropertyName("callback_query")]
        public TelegramCallbackQueryDto? CallbackQuery { get; set; }
    }

    public class TelegramMessageDto
    {
        [JsonPropertyName("message_id")]
        public long MessageId { get; set; }

        [JsonPropertyName("text")]
        public string? Text { get; set; }

        [JsonPropertyName("chat")]
        public TelegramChatDto? Chat { get; set; }

        [JsonPropertyName("from")]
        public TelegramUserDto? From { get; set; }
    }

    public class TelegramChatDto
    {
        [JsonPropertyName("id")]
        public long Id { get; set; }
    }

    public class TelegramUserDto
    {
        [JsonPropertyName("id")]
        public long Id { get; set; }

        [JsonPropertyName("username")]
        public string? Username { get; set; }
    }

    public class TelegramCallbackQueryDto
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("data")]
        public string? Data { get; set; }

        [JsonPropertyName("message")]
        public TelegramMessageDto? Message { get; set; }

        [JsonPropertyName("from")]
        public TelegramUserDto? From { get; set; }
    }

    public class TelegramLinkCodeResult
    {
        public string Code { get; set; } = string.Empty;
        public string DeepLink { get; set; } = string.Empty;
        // null = kod vaxt limitsizdir (istifadəyə və ya yeni kod yaradılana qədər qüvvədə)
        public int? ExpiresInSeconds { get; set; }
    }

    // Telegram Bot API standart cavab zərfi: { ok, result, description }
    public class TelegramApiResponse<T>
    {
        [JsonPropertyName("ok")]
        public bool Ok { get; set; }

        [JsonPropertyName("result")]
        public T? Result { get; set; }

        [JsonPropertyName("description")]
        public string? Description { get; set; }
    }

    public class TelegramBotInfoDto
    {
        [JsonPropertyName("id")]
        public long Id { get; set; }

        [JsonPropertyName("username")]
        public string? Username { get; set; }

        [JsonPropertyName("first_name")]
        public string? FirstName { get; set; }
    }

    public class TelegramWebhookInfoDto
    {
        [JsonPropertyName("url")]
        public string? Url { get; set; }

        [JsonPropertyName("pending_update_count")]
        public int PendingUpdateCount { get; set; }

        [JsonPropertyName("last_error_date")]
        public long? LastErrorDate { get; set; }

        [JsonPropertyName("last_error_message")]
        public string? LastErrorMessage { get; set; }
    }
}

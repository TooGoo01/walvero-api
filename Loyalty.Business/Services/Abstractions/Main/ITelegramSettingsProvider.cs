namespace Loyalty.Business.Services.Abstractions.Main
{
    // AppSettings cədvəlindəki açarlar; boşdursa IConfiguration-a (env var) düşür
    public static class TelegramSettingKeys
    {
        public const string BotToken = "Telegram:BotToken";
        public const string BotUsername = "Telegram:BotUsername";
        public const string WebhookSecret = "Telegram:WebhookSecret";
    }

    public sealed record TelegramSettings(string BotToken, string BotUsername, string WebhookSecret);

    public interface ITelegramSettingsProvider
    {
        Task<TelegramSettings> GetAsync(CancellationToken ct = default);
        void Invalidate();
    }
}

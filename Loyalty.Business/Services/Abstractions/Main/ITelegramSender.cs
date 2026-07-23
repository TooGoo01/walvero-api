using Loyalty.Business.Dtos.Telegram;

namespace Loyalty.Business.Services.Abstractions.Main
{
    public interface ITelegramSender
    {
        Task SendMessageAsync(long chatId, string text, object? replyMarkup = null, CancellationToken ct = default);
        Task AnswerCallbackQueryAsync(string callbackQueryId, CancellationToken ct = default);

        // Super admin idarəetməsi üçün — null = token yoxdur və ya şəbəkə xətası
        Task<TelegramApiResponse<TelegramBotInfoDto>?> GetMeAsync(CancellationToken ct = default);
        Task<TelegramApiResponse<bool>?> SetWebhookAsync(string url, string secret, CancellationToken ct = default);
        Task<TelegramApiResponse<TelegramWebhookInfoDto>?> GetWebhookInfoAsync(CancellationToken ct = default);
    }
}

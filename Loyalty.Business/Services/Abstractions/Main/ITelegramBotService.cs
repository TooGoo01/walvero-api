using Loyalty.Business.Dtos.Telegram;

namespace Loyalty.Business.Services.Abstractions.Main
{
    public interface ITelegramBotService
    {
        Task<TelegramLinkCodeResult> GenerateLinkCodeAsync(int applicationUserId, int tenantId, CancellationToken ct = default);
        Task<bool> HasActiveLinkAsync(int applicationUserId, CancellationToken ct = default);
        Task UnlinkUserAsync(int applicationUserId, CancellationToken ct = default);
        Task HandleUpdateAsync(TelegramUpdateDto update, CancellationToken ct = default);
    }
}

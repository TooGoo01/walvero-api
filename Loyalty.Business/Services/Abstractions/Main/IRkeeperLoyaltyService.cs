using Loyalty.Business.Models.TenantIntegration;

namespace Loyalty.Business.Services.Abstractions.Main
{
    /// <summary>
    /// r_keeper 7 FarCards HTTP (ExtDllHTTP.dll) — Walvero kart serveri kimi.
    /// Kassa sinxron çağırır: kart skanında GetCardInfoEx, çek bağlananda / ləğvində TransactionsEx.
    /// Hər iki metod hazır FarCards XML cavabı qaytarır.
    /// </summary>
    public interface IRkeeperLoyaltyService
    {
        Task<string> GetCardInfoAsync(WalletWebhookResolvedContext context, string requestXml, CancellationToken ct = default);

        Task<string> ProcessTransactionsAsync(WalletWebhookResolvedContext context, string requestXml, CancellationToken ct = default);
    }
}

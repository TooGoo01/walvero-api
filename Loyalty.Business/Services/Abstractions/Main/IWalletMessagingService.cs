using Loyalty.Business.Results;
using System.Threading;
using System.Threading.Tasks;

namespace Loyalty.Business.Services.Abstractions.Main
{
    /// <summary>Wallet push nəticəsi: Cards = aktiv kart sayı (hədəf), Delivered = uğurlu Google + Apple kanal sayı.</summary>
    public sealed record WalletPushResult(int Cards, int GoogleOk, int AppleOk)
    {
        public int Delivered => GoogleOk + AppleOk;
        public static readonly WalletPushResult Empty = new(0, 0, 0);
    }

    public interface IWalletMessagingService
    {
        /// <summary>Müştərinin bütün aktiv kartlarına push — struktur nəticə (tenant bildiriş statistikası üçün).</summary>
        Task<WalletPushResult> PushToCustomerAsync(int customerId, string title, string body, CancellationToken ct = default);
        Task<ServiceResult> SendToCardAsync(int loyaltyCardId, string title, string body, CancellationToken ct = default);
        Task<ServiceResult> SendToUserAsync(int applicationUserId, string title, string body, CancellationToken ct = default);
        Task<ServiceResult> SendToCustomerAsync(int customerId, string title, string body, CancellationToken ct = default);
        Task<ServiceResult> SendToTenantAsync(int tenantId, string title, string body, CancellationToken ct = default);
        Task<ServiceResult> SendToAllAsync(string title, string body, CancellationToken ct = default);
    }
}

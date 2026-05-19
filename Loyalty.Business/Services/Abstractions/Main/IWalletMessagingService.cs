using Loyalty.Business.Results;
using System.Threading;
using System.Threading.Tasks;

namespace Loyalty.Business.Services.Abstractions.Main
{
    public interface IWalletMessagingService
    {
        Task<ServiceResult> SendToCardAsync(int loyaltyCardId, string title, string body, CancellationToken ct = default);
        Task<ServiceResult> SendToUserAsync(int applicationUserId, string title, string body, CancellationToken ct = default);
        Task<ServiceResult> SendToCustomerAsync(int customerId, string title, string body, CancellationToken ct = default);
        Task<ServiceResult> SendToTenantAsync(int tenantId, string title, string body, CancellationToken ct = default);
        Task<ServiceResult> SendToAllAsync(string title, string body, CancellationToken ct = default);
    }
}

using Loyalty.Business.Dtos.Post.Main;
using Loyalty.Business.Results;

namespace Loyalty.Business.Services.Abstractions.Main
{
    /// <summary>
    /// Service for generating wallet passes for Walvero shadow tenant.
    /// These passes are based on user's WalveroId, not loyalty cards.
    /// </summary>
    public interface IWalveroWalletPassService
    {
        /// <summary>
        /// Create or retrieve a wallet pass for a user's WalveroId.
        /// </summary>
        /// <param name="userId">Application user ID</param>
        /// <param name="platform">"ios" or "android"</param>
        /// <param name="ct">Cancellation token</param>
        /// <returns>ServiceResult with pass URLs and metadata</returns>
        Task<ServiceResult<WalveroWalletPassResponseDto>> CreateWalveroPassAsync(
            int userId,
            string platform,
            CancellationToken ct = default);
    }
}


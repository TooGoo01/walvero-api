using Loyalty.Business.Dtos.Post.Main;
using Loyalty.Business.Results;
using Loyalty.DataAccess.Enums;


namespace Loyalty.Business.Services.Abstractions.Main
{
    public interface IWalletRouter
    {
        Task<ServiceResult> RouteAsync(WalletPlatform platform, int cardId,
            CardTemplateSnapshot tpl, decimal currentPoints, int currentSteps, int completedCycles, int delta, CancellationToken ct);
    }
}

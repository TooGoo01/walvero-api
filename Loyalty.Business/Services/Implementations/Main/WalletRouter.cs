using Loyalty.Business.Dtos.Post.Main;
using Loyalty.Business.Results;
using Loyalty.Business.Services.Abstractions.Main;
using Loyalty.DataAccess.Enums;


namespace Loyalty.Business.Services.Implementations.Main
{
    public sealed class WalletRouter : IWalletRouter
    {
        private readonly GoogleWalletRedeemer _g; private readonly AppleWalletRedeemer _a;
        public WalletRouter(GoogleWalletRedeemer g, AppleWalletRedeemer a) { _g = g; _a = a; }

        public Task<ServiceResult> RouteAsync(WalletPlatform p, int id, CardTemplateSnapshot tpl,
            int pts, int steps, int completedCycles, int delta, CancellationToken ct,
            int lotteryTickets = 0, string? lotteryWinPrize = null, DateTime? lotteryDrawDate = null)
            => p switch
            {
                WalletPlatform.GooglePay => _g.RedeemByCardIdAsync(id, tpl, pts, steps, delta, ct, lotteryTickets, lotteryWinPrize, lotteryDrawDate),
                WalletPlatform.AppleWallet => _a.RedeemByCardIdAsync(id, tpl, pts, steps, delta, ct, lotteryTickets, lotteryWinPrize, lotteryDrawDate),
                _ => Task.FromResult(new ServiceResult(false, "Wallet platformu müəyyən olunmadı."))
            };
    }
}

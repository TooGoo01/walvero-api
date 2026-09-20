using Hangfire;
using Loyalty.Business.Helpers;
using Loyalty.Business.Services.Abstractions.Main;
using Loyalty.DataAccess.Entities.Main;
using Loyalty.DataAccess.Enums;
using Loyalty.DataAccess.Repositories.Abstractions.Main;
using Microsoft.Extensions.Logging;

namespace Loyalty.Business.Services.Implementations.Main
{
    /// <summary>
    /// Wallet pass push-u Hangfire background-da icra edir (webhook-u bloklamasın).
    /// Kart/proqram/branding ID ilə reload olunur — job öz DI scope-unda işləyir.
    /// </summary>
    public class WalletPushJob : IWalletPushJob
    {
        private readonly ILoyaltyCardRepository _cardRepo;
        private readonly ILoyaltyProgramRepository _programRepo;
        private readonly ITenantBrandingRepository _brandingRepo;
        private readonly IWalletRouter _walletRouter;
        private readonly ILogger<WalletPushJob> _logger;

        public WalletPushJob(
            ILoyaltyCardRepository cardRepo,
            ILoyaltyProgramRepository programRepo,
            ITenantBrandingRepository brandingRepo,
            IWalletRouter walletRouter,
            ILogger<WalletPushJob> logger)
        {
            _cardRepo = cardRepo;
            _programRepo = programRepo;
            _brandingRepo = brandingRepo;
            _walletRouter = walletRouter;
            _logger = logger;
        }

        // Push uğursuz olsa Hangfire 3 dəfə yenidən cəhd etsin.
        [AutomaticRetry(Attempts = 3, OnAttemptsExceeded = AttemptsExceededAction.Fail)]
        [Queue("default")]
        public async Task RunAsync(int cardId, int loyaltyProgramId, decimal delta)
        {
            var card = await _cardRepo.GetById(cardId, CancellationToken.None);
            if (card == null)
            {
                _logger.LogWarning("WalletPushJob: card not found. CardId={CardId}", cardId);
                return;
            }

            var program = await _programRepo.GetByIdNameAsync(loyaltyProgramId);
            if (program?.CardTemplate == null)
            {
                _logger.LogDebug("WalletPushJob: program/CardTemplate null. CardId={CardId}", cardId);
                return;
            }

            var branding = await _brandingRepo.GetByTenantIdAsync(card.TenantId, CancellationToken.None);
            const string baseUrl = "https://appapi.walvero.com";
            string ToAbs(string? dbPath)
            {
                if (string.IsNullOrWhiteSpace(dbPath)) return string.Empty;
                if (dbPath.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                    dbPath.StartsWith("https://", StringComparison.OrdinalIgnoreCase)) return dbPath;
                var normalizedBaseUrl = baseUrl.TrimEnd('/', '\\');
                var normalizedPath = dbPath.TrimStart('~').TrimStart('/', '\\');
                normalizedPath = string.IsNullOrWhiteSpace(normalizedPath) ? "/" : "/" + normalizedPath;
                return normalizedBaseUrl + normalizedPath;
            }

            var snapshot = CardTemplateSnapshotFactory.From(program, program.CardTemplate, branding, ToAbs);
            var primaryPlatform = card.IsIOS ? WalletPlatform.AppleWallet : WalletPlatform.GooglePay;
            var secondaryPlatform = card.IsIOS ? WalletPlatform.GooglePay : WalletPlatform.AppleWallet;
            var intDelta = (int)delta;

            _logger.LogInformation("WalletPushJob: PRIMARY {Platform}. CardId={CardId}", primaryPlatform, cardId);
            try
            {
                var primaryRes = await _walletRouter.RouteAsync(
                    primaryPlatform, card.Id, snapshot,
                    card.CurrentPoints, card.CurrentSteps, card.CompletedCycles, intDelta,
                    CancellationToken.None);
                if (!primaryRes.Success)
                    _logger.LogWarning("WalletPushJob: PRIMARY {Platform} failed. CardId={CardId}", primaryPlatform, cardId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "WalletPushJob: PRIMARY {Platform} error. CardId={CardId}", primaryPlatform, cardId);
            }

            _logger.LogInformation("WalletPushJob: SECONDARY {Platform}. CardId={CardId}", secondaryPlatform, cardId);
            try
            {
                var secondaryRes = await _walletRouter.RouteAsync(
                    secondaryPlatform, card.Id, snapshot,
                    card.CurrentPoints, card.CurrentSteps, card.CompletedCycles, intDelta,
                    CancellationToken.None);
                if (!secondaryRes.Success)
                    _logger.LogWarning("WalletPushJob: SECONDARY {Platform} failed. CardId={CardId}", secondaryPlatform, cardId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "WalletPushJob: SECONDARY {Platform} error. CardId={CardId}", secondaryPlatform, cardId);
            }
        }
    }
}

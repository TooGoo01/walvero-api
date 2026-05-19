using Loyalty.Business.Dtos.Post.Main;
using Loyalty.Business.Results;
using Loyalty.Business.Services.Abstractions.Main;
using Loyalty.DataAccess.Context;
using Loyalty.DataAccess.Entities.Main;
using Loyalty.DataAccess.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Loyalty.Business.Services.Implementations.Main
{
    public sealed class WalletMessagingService : IWalletMessagingService
    {
        private readonly LoyaltyWalletDbContext _db;
        private readonly IGoogleWalletServiceFactory _googleFactory;
        private readonly IPasskitPushService _applePush;
        private readonly IWalletCredentialProvider _credProvider;
        private readonly ILogger<WalletMessagingService> _log;

        public WalletMessagingService(
            LoyaltyWalletDbContext db,
            IGoogleWalletServiceFactory googleFactory,
            IPasskitPushService applePush,
            IWalletCredentialProvider credProvider,
            ILogger<WalletMessagingService> log)
        {
            _db = db;
            _googleFactory = googleFactory;
            _applePush = applePush;
            _credProvider = credProvider;
            _log = log;
        }

        public async Task<ServiceResult> SendToCardAsync(int loyaltyCardId, string title, string body, CancellationToken ct = default)
        {
            var card = await _db.LoyaltyCards
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == loyaltyCardId, ct);

            if (card == null)
                return new ServiceResult(false, $"LoyaltyCard {loyaltyCardId} not found");

            var deliveries = await PushToCardsAsync(new[] { card }, title, body, ct);
            return new ServiceResult(true, deliveries, $"Wallet message delivered to {deliveries} channel(s).");
        }

        public async Task<ServiceResult> SendToUserAsync(int applicationUserId, string title, string body, CancellationToken ct = default)
        {
            var cards = await _db.LoyaltyCards
                .AsNoTracking()
                .Where(c => c.Status == CardStatus.Active
                         && c.Customer.applicationUserId == applicationUserId)
                .ToListAsync(ct);

            if (cards.Count == 0)
                return new ServiceResult(true, 0, "User has no active wallet cards.");

            var deliveries = await PushToCardsAsync(cards, title, body, ct);
            return new ServiceResult(true, deliveries, $"Wallet message delivered to {deliveries} channel(s) across {cards.Count} card(s).");
        }

        public async Task<ServiceResult> SendToTenantAsync(int tenantId, string title, string body, CancellationToken ct = default)
        {
            var cards = await _db.LoyaltyCards
                .AsNoTracking()
                .Where(c => c.TenantId == tenantId && c.Status == CardStatus.Active)
                .ToListAsync(ct);

            if (cards.Count == 0)
                return new ServiceResult(true, 0, "Tenant has no active wallet cards.");

            var deliveries = await PushToCardsAsync(cards, title, body, ct);
            return new ServiceResult(true, deliveries, $"Wallet message delivered to {deliveries} channel(s) across {cards.Count} card(s).");
        }

        public async Task<ServiceResult> SendToAllAsync(string title, string body, CancellationToken ct = default)
        {
            var cards = await _db.LoyaltyCards
                .AsNoTracking()
                .Where(c => c.Status == CardStatus.Active)
                .ToListAsync(ct);

            if (cards.Count == 0)
                return new ServiceResult(true, 0, "No active wallet cards in the system.");

            var deliveries = await PushToCardsAsync(cards, title, body, ct);
            return new ServiceResult(true, deliveries, $"Wallet message delivered to {deliveries} channel(s) across {cards.Count} card(s).");
        }

        // Returns how many wallet pushes were attempted (Android + iOS combined).
        private async Task<int> PushToCardsAsync(IEnumerable<LoyaltyCard> cards, string title, string body, CancellationToken ct)
        {
            int delivered = 0;

            // Group by tenant+program so we can reuse a single GoogleWalletService per group.
            foreach (var group in cards.GroupBy(c => new { c.TenantId, c.LoyaltyProgramId }))
            {
                IGoogleWalletService? gw = null;
                AppleWalletRuntimeCreds? appleCreds = null;

                foreach (var card in group)
                {
                    if (card.IsAndroid)
                    {
                        try
                        {
                            gw ??= await _googleFactory.CreateForTenantAsync(group.Key.TenantId, group.Key.LoyaltyProgramId, ct);
                            var objectId = gw.BuildObjectIdForCard(group.Key.TenantId, group.Key.LoyaltyProgramId, card.Id);
                            var res = await gw.AddMessageToObjectAsync(objectId, title, body, ct);
                            if (res.Success) delivered++;
                        }
                        catch (Exception ex)
                        {
                            _log.LogWarning(ex, "Google Wallet push failed. CardId={CardId}", card.Id);
                        }
                    }

                    if (card.IsIOS && !string.IsNullOrWhiteSpace(card.AppleSerial))
                    {
                        try
                        {
                            appleCreds ??= await _credProvider.GetAppleAsync(group.Key.TenantId, ct);
                            if (appleCreds != null && !string.IsNullOrWhiteSpace(appleCreds.PassTypeIdentifier))
                            {
                                await _applePush.NotifyUpdatedMessageAsync(
                                    group.Key.TenantId,
                                    appleCreds.PassTypeIdentifier,
                                    card.AppleSerial!,
                                    title,
                                    body,
                                    ct);
                                delivered++;
                            }
                        }
                        catch (Exception ex)
                        {
                            _log.LogWarning(ex, "Apple Wallet push failed. CardId={CardId}", card.Id);
                        }
                    }
                }
            }

            return delivered;
        }
    }
}


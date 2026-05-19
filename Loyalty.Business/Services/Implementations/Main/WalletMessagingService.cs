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
        private readonly IAppleWalletService _appleService;
        private readonly ILogger<WalletMessagingService> _log;

        public WalletMessagingService(
            LoyaltyWalletDbContext db,
            IGoogleWalletServiceFactory googleFactory,
            IAppleWalletService appleService,
            ILogger<WalletMessagingService> log)
        {
            _db = db;
            _googleFactory = googleFactory;
            _appleService = appleService;
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

        public async Task<ServiceResult> SendToCustomerAsync(int customerId, string title, string body, CancellationToken ct = default)
        {
            var cards = await _db.LoyaltyCards
                .AsNoTracking()
                .Where(c => c.CustomerId == customerId && c.Status == CardStatus.Active)
                .ToListAsync(ct);

            if (cards.Count == 0)
                return new ServiceResult(true, 0, "Customer has no active wallet cards.");

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

            foreach (var group in cards.GroupBy(c => new { c.TenantId, c.LoyaltyProgramId }))
            {
                IGoogleWalletService? gw = null;

                foreach (var card in group)
                {
                    // Always attempt Google push — IsAndroid flag is set only when the user clicks
                    // "Save to Google Wallet", but the LoyaltyObject may exist on Google's side
                    // (e.g. user enrolled via web). AddMessageToObjectAsync returns silently with
                    // "Object not found" if the pass is not on Google yet.
                    try
                    {
                        gw ??= await _googleFactory.CreateForTenantAsync(group.Key.TenantId, group.Key.LoyaltyProgramId, ct);
                        var objectId = gw.BuildObjectIdForCard(group.Key.TenantId, group.Key.LoyaltyProgramId, card.Id);
                        _log.LogInformation("[GW-TRY] Sending addmessage to Google Wallet. CardId={CardId} ObjectId={ObjectId}", card.Id, objectId);
                        var res = await gw.AddMessageToObjectAsync(objectId, title, body, ct);
                        if (res.Success) delivered++;
                        else _log.LogInformation("[GW-TRY] Google push skipped. CardId={CardId} Reason={Reason}", card.Id, res.Message);
                    }
                    catch (Exception ex)
                    {
                        _log.LogWarning(ex, "Google Wallet push failed. CardId={CardId}", card.Id);
                    }

                    if (card.IsIOS && !string.IsNullOrWhiteSpace(card.AppleSerial))
                    {
                        try
                        {
                            // Rebuilds pass with notification backField + changeMessage,
                            // signs, saves, and triggers APNs refetch — iOS surfaces a
                            // lock-screen notification when it pulls the new pass.
                            var res = await _appleService.PushNotificationToCardAsync(card.Id, title, body, ct);
                            if (res.Success) delivered++;
                            else _log.LogWarning("Apple Wallet push failed. CardId={CardId} Reason={Reason}", card.Id, res.Message);
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


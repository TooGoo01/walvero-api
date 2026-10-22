using Hangfire;
using Loyalty.Business.Dtos.Post.Main;
using Loyalty.Business.Helpers;
using Loyalty.Business.Services.Abstractions.Main;
using Loyalty.DataAccess.Context;
using Loyalty.DataAccess.Entities.Main;
using Loyalty.DataAccess.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Loyalty.Business.Services.Implementations.Main
{
    public sealed class IikoLegacyBalanceService : IIikoLegacyBalanceService
    {
        private const int SystemUserId = 1;

        private readonly IIikoPosApiClient _iiko;
        private readonly LoyaltyWalletDbContext _db;
        private readonly ILogger<IikoLegacyBalanceService> _logger;

        public IikoLegacyBalanceService(IIikoPosApiClient iiko, LoyaltyWalletDbContext db, ILogger<IikoLegacyBalanceService> logger)
        {
            _iiko = iiko;
            _db = db;
            _logger = logger;
        }

        public async Task<IikoPhoneMatch?> FindByPhoneAsync(TenantIntegration integration, string accessToken, Guid orgId, Customer customer, CancellationToken ct = default)
        {
            var phone = PhoneNormalizer.ToE164(customer.PhoneNumber);
            if (string.IsNullOrEmpty(phone)) return null;

            var r = await _iiko.FindCustomerAsync(accessToken, orgId, IikoCustomerLookup.Phone, phone, ct);
            if (!r.Success || r.Data == null)
            {
                if (!r.IsNotFound)
                    _logger.LogWarning("iiko phone match: lookup failed. CustomerId={CustomerId}, orgId={OrgId}, Status={Status}, Error={Error}",
                        customer.Id, orgId, r.StatusCode, r.ErrorDescription);
                return null;
            }

            var iikoId = r.Data.Id;
            var iikoIdStr = iikoId.ToString();
            var conflict = await _db.Set<PosCustomerMapping>().AsNoTracking()
                .Where(m => m.TenantIntegrationId == integration.Id && m.ExternalCustomerId == iikoIdStr && m.CustomerId != customer.Id)
                .Select(m => (int?)m.CustomerId)
                .FirstOrDefaultAsync(ct);
            if (conflict.HasValue)
            {
                _logger.LogWarning("iiko phone match: iiko qonağı başqa Walvero müştərisinə bağlıdır. CustomerId={CustomerId}, iikoId={IikoId}, OtherCustomerId={Other}",
                    customer.Id, iikoId, conflict);
                return new IikoPhoneMatch(iikoId, null, 0m, conflict);
            }

            // Proqramın cüzdanı (PosWalletId) — PosSyncService ilə eyni seçim qaydası; kart hələ yoxdursa birinci cüzdan
            var preferredWallet = await _db.Set<LoyaltyCard>().AsNoTracking()
                .Where(c => c.TenantId == customer.TenantId && c.CustomerId == customer.Id && c.IsActive)
                .OrderByDescending(c => c.Id)
                .Select(c => c.LoyaltyProgram.PosWalletId)
                .FirstOrDefaultAsync(ct);
            var wallets = (r.Data.WalletBalances ?? new List<IikoWalletBalance>()).Select(w => (w.Id, w.Balance)).ToList();
            var pick = IikoWalletSelection.Pick(wallets, preferredWallet);
            var found = pick.Outcome == IikoWalletSelection.Outcome.Found;
            var balance = found ? IikoLegacyBalance.Normalize(pick.Balance) : 0m;

            _logger.LogInformation("iiko phone match: CustomerId={CustomerId}, orgId={OrgId}, iikoId={IikoId}, Wallet={Wallet} ({Outcome}), Balance={Balance}",
                customer.Id, orgId, iikoId, pick.WalletId, pick.Outcome, balance);
            return new IikoPhoneMatch(iikoId, found ? pick.WalletId : null, balance, null);
        }

        public async Task<decimal> ImportPendingAsync(int tenantId, int customerId, CancellationToken ct = default)
        {
            var pending = await _db.Set<PosCustomerMapping>()
                .Include(m => m.TenantIntegrationVenue)
                .Where(m => m.CustomerId == customerId && m.IsActive && m.LegacyBalance > 0 && m.LegacyImportedAt == null)
                .ToListAsync(ct);
            if (pending.Count == 0) return 0m;

            var card = await _db.Set<LoyaltyCard>()
                .Where(c => c.TenantId == tenantId && c.CustomerId == customerId && c.IsActive)
                .OrderByDescending(c => c.Id)
                .FirstOrDefaultAsync(ct);
            if (card == null)
            {
                _logger.LogInformation("iiko legacy import: aktiv kart yoxdur, gözləyir. CustomerId={CustomerId}", customerId);
                return 0m;
            }

            var template = await _db.Set<LoyaltyProgram>().AsNoTracking()
                .Where(p => p.Id == card.LoyaltyProgramId)
                .Select(p => p.CardTemplate)
                .FirstOrDefaultAsync(ct);
            var templateType = template?.EffectiveType ?? CardTemplateType.PointsBased;
            var now = DateTime.UtcNow;
            var total = 0m;

            foreach (var g in pending.GroupBy(m => IikoLegacyBalance.OrderRef(m.ExternalCustomerId, m.LegacyWalletId)))
            {
                var orderRef = g.Key;
                var amount = IikoLegacyBalance.Normalize(g.Max(m => m.LegacyBalance));

                if (templateType == CardTemplateType.ProgressBased)
                {
                    // Möhür kartına pul balansı köçürülmür — yalnız işarələnir ki, balans push bloklanmasın
                    _logger.LogWarning("iiko legacy import skipped (ProgressBased). CustomerId={CustomerId}, CardId={CardId}, Amount={Amount}", customerId, card.Id, amount);
                }
                else
                {
                    var already = await _db.Set<Transaction>().AsNoTracking()
                        .AnyAsync(t => t.OrderId == orderRef && t.LoyaltyCard.CustomerId == customerId, ct);
                    if (!already && amount > 0)
                    {
                        BalanceMath.ApplyDelta(card, new CardTemplateSnapshot { TemplateType = templateType, TotalSteps = template?.TotalSteps ?? 10 }, amount);
                        card.LifetimePoints += amount;
                        card.EditDate = now;
                        _db.Set<Transaction>().Add(new Transaction
                        {
                            LoyaltyCardId = card.Id,
                            Type = TransactionType.Adjusted,
                            Status = RedeemStatus.Applied,
                            Points = amount,
                            BalanceAfter = card.CurrentPoints,
                            Description = "Kassa köhnə balansı (telefonla birləşdirmə)",
                            OrderId = orderRef,
                            BranchId = g.Select(m => m.TenantIntegrationVenue?.BranchId).FirstOrDefault(b => b.HasValue),
                            RegUserId = SystemUserId,
                            RegDate = now,
                            IsActive = true,
                            LocalOccurredAt = DateTimeOffset.UtcNow,
                            TimeZoneIdUsed = "UTC"
                        });
                        total += amount;
                    }
                    else if (already)
                        _logger.LogInformation("iiko legacy import: artıq tətbiq olunub. CustomerId={CustomerId}, Ref={Ref}", customerId, orderRef);
                }

                foreach (var m in g) m.LegacyImportedAt = now;
            }

            await _db.SaveChangesAsync(ct);
            _logger.LogInformation("iiko legacy import: CustomerId={CustomerId}, CardId={CardId}, Added={Added}, NewBalance={Balance}",
                customerId, card.Id, total, card.CurrentPoints);

            if (total > 0)
            {
                try
                {
                    BackgroundJob.Enqueue<IWalletPushJob>(j => j.RunAsync(card.Id, card.LoyaltyProgramId, total));
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "iiko legacy import: wallet push enqueue failed. CardId={CardId}", card.Id);
                }
            }
            return total;
        }
    }
}

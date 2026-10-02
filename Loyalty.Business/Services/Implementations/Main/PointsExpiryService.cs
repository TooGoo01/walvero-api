using Hangfire;
using Loyalty.Business.Helpers;
using Loyalty.Business.Services.Abstractions.Main;
using Loyalty.DataAccess.Context;
using Loyalty.DataAccess.Entities.Main;
using Loyalty.DataAccess.Enums;
using Loyalty.DataAccess.UnitOfWorks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Loyalty.Business.Services.Implementations.Main
{
    public sealed class PointsExpiryService : IPointsExpiryService
    {
        private const int BatchSize = 200;
        private const int SystemUserId = 1;

        private readonly LoyaltyWalletDbContext _db;
        private readonly IUnitOfWork _uow;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ITimeZoneService _timeZoneService;
        private readonly ILogger<PointsExpiryService> _logger;

        public PointsExpiryService(
            LoyaltyWalletDbContext db,
            IUnitOfWork uow,
            IServiceScopeFactory scopeFactory,
            ITimeZoneService timeZoneService,
            ILogger<PointsExpiryService> logger)
        {
            _db = db;
            _uow = uow;
            _scopeFactory = scopeFactory;
            _timeZoneService = timeZoneService;
            _logger = logger;
        }

        // Hangfire: bütün batch bir işdir — kart-kart emal, öz try/catch; auto-retry lazım deyil (növbəti run götürür).
        [AutomaticRetry(Attempts = 0)]
        [Queue("default")]
        public async Task RunAsync(CancellationToken ct = default)
        {
            // Müddətli bonuslar (ad günü hədiyyəsi) — yalnız istifadə olunmamış hissə silinir
            try { await ExpireBonusGrantsAsync(ct); }
            catch (Exception ex) { _logger.LogError(ex, "Bonus grant expiry run failed"); }

            var now = DateTime.UtcNow;
            var due = await _db.Set<LoyaltyCard>()
                .Where(c => c.IsActive && c.ExpiresAt != null && c.ExpiresAt <= now)
                .OrderBy(c => c.Id)
                .Take(BatchSize)
                .ToListAsync(ct);
            if (due.Count == 0) return;

            _logger.LogInformation("Points expiry: {Count} due", due.Count);

            var zeroed = 0; var synced = 0;
            foreach (var card in due)
            {
                try
                {
                    var (points, ok) = await ExpireCoreAsync(card, ct);
                    if (points != 0) zeroed++;
                    if (ok) synced++;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Points expiry failed. CardId={CardId}", card.Id);
                    _db.Entry(card).State = EntityState.Detached;
                }
            }

            _logger.LogInformation("Points expiry done: due={Due}, zeroed={Zeroed}, synced={Synced}", due.Count, zeroed, synced);
        }

        public async Task<decimal> ExpireCardAsync(int cardId, CancellationToken ct = default)
        {
            var card = await _db.Set<LoyaltyCard>().FirstOrDefaultAsync(c => c.Id == cardId, ct);
            if (card == null || !card.IsPointsExpired()) return 0m;
            var (points, _) = await ExpireCoreAsync(card, ct);
            return points;
        }

        /// <summary>Balansı sıfırla (varsa) + iiko sync; sync uğurludursa ExpiresAt təmizlənir. Qaytarır (sıfırlanan xal, sync ok).</summary>
        private async Task<(decimal Points, bool Synced)> ExpireCoreAsync(LoyaltyCard card, CancellationToken ct)
        {
            var now = DateTime.UtcNow;
            var expiresAt = card.ExpiresAt ?? now;
            decimal old = 0m;

            if (card.CurrentPoints > 0 || card.CurrentSteps > 0)
            {
                old = card.CurrentPoints;
                card.CurrentPoints = 0;
                card.CurrentSteps = 0;
                card.EditDate = now;
                card.EditUserId = SystemUserId;
                _uow.Repository<LoyaltyCard>().Update(card);

                // Təsvirdə tenant-ın yerli vaxtı; alınmasa UTC
                string when;
                try
                {
                    var (local, _) = await _timeZoneService.GetTenantLocalTimeAsync(card.TenantId, DateTime.SpecifyKind(expiresAt, DateTimeKind.Utc), ct);
                    when = local.ToString("dd.MM.yyyy HH:mm");
                }
                catch { when = expiresAt.ToString("dd.MM.yyyy HH:mm") + " UTC"; }

                await _uow.Repository<Transaction>().AddAsync(new Transaction
                {
                    LoyaltyCardId = card.Id,
                    Type = TransactionType.Expired,
                    Status = RedeemStatus.Applied,
                    Points = -old,
                    BalanceAfter = 0,
                    Description = $"Xalın istifadə müddəti bitdi ({when})",
                    OrderId = $"expire:{card.Id}:{expiresAt:yyyyMMddHHmm}",
                    RegUserId = SystemUserId,
                    RegDate = now,
                    IsActive = true,
                    LocalOccurredAt = DateTimeOffset.UtcNow,
                    TimeZoneIdUsed = "UTC"
                });
                await _uow.CommitAsync(ct);

                _logger.LogInformation("Points expired. CardId={CardId}, CustomerId={CustomerId}, Points={Points}, ExpiresAt={ExpiresAt}",
                    card.Id, card.CustomerId, old, expiresAt);

                try
                {
                    BackgroundJob.Enqueue<IWalletPushJob>(j => j.RunAsync(card.Id, card.LoyaltyProgramId, -old));
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Points expiry: wallet push enqueue failed. CardId={CardId}", card.Id);
                }
            }

            // iiko wallet → 0 (chargeoff). Uğurlu olanda tarix silinir; yoxsa növbəti run təkrar sync edir.
            var synced = await SyncPosSafeAsync(card.TenantId, card.CustomerId, ct);
            if (synced)
            {
                card.ExpiresAt = null;
                card.EditDate = DateTime.UtcNow;
                _uow.Repository<LoyaltyCard>().Update(card);
                await _uow.CommitAsync(ct);
            }
            else
            {
                _logger.LogWarning("Points expiry: POS sync not confirmed, ExpiresAt kept for retry. CardId={CardId}", card.Id);
            }

            return (old, synced);
        }

        /// <summary>
        /// Vaxtı keçmiş BonusGrant-lar: istifadə olunmamış hissə = clamp(Amount − grant-dan sonrakı xərclər, 0, CurrentPoints)
        /// ("bonus əvvəl xərclənir") → Expired tx + iiko sync + wallet push. Yalnız xal kartları (möhür hədiyyəsi müddətsizdir).
        /// </summary>
        public async Task ExpireBonusGrantsAsync(CancellationToken ct = default)
        {
            var now = DateTime.UtcNow;
            var grants = await _db.Set<BonusGrant>()
                .Where(g => g.IsActive && g.Status == BonusGrantStatus.Active && g.ExpiresAtUtc != null && g.ExpiresAtUtc <= now)
                .OrderBy(g => g.Id)
                .Take(BatchSize)
                .ToListAsync(ct);
            foreach (var g in grants)
            {
                try
                {
                    var card = await _db.Set<LoyaltyCard>().FirstOrDefaultAsync(c => c.Id == g.LoyaltyCardId, ct);
                    if (card == null) { g.Status = BonusGrantStatus.Closed; g.EditDate = now; await _uow.CommitAsync(ct); continue; }

                    var spent = await _db.Set<Transaction>()
                        .Where(t => t.LoyaltyCardId == card.Id && t.IsActive && t.Status == RedeemStatus.Applied
                                    && t.Type == TransactionType.Redeemed && t.RegDate >= g.GrantedAtUtc)
                        .SumAsync(t => (decimal?)Math.Abs(t.Points), ct) ?? 0m;
                    var unspent = BonusExpiryMath.Unspent(g.Amount, spent, card.CurrentPoints);

                    g.Status = BonusGrantStatus.Expired;
                    g.ExpiredAmount = unspent;
                    g.EditDate = now;
                    g.EditUserId = SystemUserId;

                    if (unspent > 0)
                    {
                        card.CurrentPoints -= unspent;
                        card.EditDate = now;
                        card.EditUserId = SystemUserId;
                        _uow.Repository<LoyaltyCard>().Update(card);
                        await _uow.Repository<Transaction>().AddAsync(new Transaction
                        {
                            LoyaltyCardId = card.Id,
                            Type = TransactionType.Expired,
                            Status = RedeemStatus.Applied,
                            Points = -unspent,
                            BalanceAfter = card.CurrentPoints,
                            Description = g.Source == "birthday" ? "Ad günü hədiyyəsinin müddəti bitdi" : "Bonusun müddəti bitdi",
                            OrderId = $"grant-expire:{g.Id}",
                            RegUserId = SystemUserId,
                            RegDate = now,
                            IsActive = true,
                            LocalOccurredAt = DateTimeOffset.UtcNow,
                            TimeZoneIdUsed = "UTC"
                        });
                    }
                    await _uow.CommitAsync(ct);
                    _logger.LogInformation("Bonus grant expired. GrantId={GrantId} CardId={CardId} Amount={Amount} Spent={Spent} Removed={Removed}",
                        g.Id, card.Id, g.Amount, spent, unspent);

                    if (unspent > 0)
                    {
                        try { BackgroundJob.Enqueue<IWalletPushJob>(j => j.RunAsync(card.Id, card.LoyaltyProgramId, -unspent)); }
                        catch (Exception ex) { _logger.LogWarning(ex, "Bonus expiry wallet push enqueue failed. CardId={CardId}", card.Id); }
                        await SyncPosBalanceSafeAsync(card.TenantId, card.CustomerId, card.CurrentPoints, ct);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Bonus grant expiry failed. GrantId={GrantId}", g.Id);
                }
            }
        }

        private async Task SyncPosBalanceSafeAsync(int tenantId, int customerId, decimal points, CancellationToken ct)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var sync = scope.ServiceProvider.GetRequiredService<IPosSyncService>();
                await sync.SyncBalanceToPosAsync(tenantId, customerId, points, ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Bonus expiry POS sync failed. CustomerId={CustomerId}, TenantId={TenantId}", customerId, tenantId);
            }
        }

        private async Task<bool> SyncPosSafeAsync(int tenantId, int customerId, CancellationToken ct)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var sync = scope.ServiceProvider.GetRequiredService<IPosSyncService>();
                return await sync.SyncBalanceToPosAsync(tenantId, customerId, 0m, ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Points expiry: POS balance sync failed. CustomerId={CustomerId}, TenantId={TenantId}", customerId, tenantId);
                return false;
            }
        }
    }
}

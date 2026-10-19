using Loyalty.Business.Dtos.Post.Main;
using Loyalty.Business.Helpers;
using Loyalty.Business.Services.Abstractions.Main;
using Loyalty.DataAccess.Context;
using Loyalty.DataAccess.Entities.Main;
using Loyalty.DataAccess.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Loyalty.Business.Services.Implementations.Main
{
    /// <summary>
    /// Suspend/restore toggle-ından sonra tenant-ın bütün wallet-li kartlarına
    /// yeniləmə push edir. Mask dəyərləri burada hesablanmır — kartın CARİ state-i
    /// router-ə ötürülür, mask məntiqi redeemer-lərdədir (suspended → mask,
    /// restored → real dəyərlər). Google PATCH dərhal görünür; Apple .pkpass
    /// regen olunur və cihaz webServiceURL vasitəsilə pull edəndə yenilənir (best-effort).
    /// Tenant IsActive=false olsa belə işləyir — RedeemService-dən asılı deyil.
    /// </summary>
    public class TenantSuspensionPassRefresher : ITenantSuspensionPassRefresher
    {
        private readonly LoyaltyWalletDbContext _dbContext;
        private readonly IWalletRouter _walletRouter;
        private readonly IConfiguration _config;
        private readonly IGoogleWalletServiceFactory _googleFactory;
        private readonly ILogger<TenantSuspensionPassRefresher> _logger;

        public TenantSuspensionPassRefresher(
            LoyaltyWalletDbContext dbContext,
            IWalletRouter walletRouter,
            IConfiguration config,
            IGoogleWalletServiceFactory googleFactory,
            ILogger<TenantSuspensionPassRefresher> logger)
        {
            _dbContext = dbContext;
            _walletRouter = walletRouter;
            _config = config;
            _googleFactory = googleFactory;
            _logger = logger;
        }

        // Növbədəki köhnə Hangfire işləri bu imza ilə gəlir — saxlanır
        public Task RefreshTenantPassesAsync(int tenantId, CancellationToken ct = default)
            => RefreshProgramPassesAsync(tenantId, null, ct);

        public async Task RefreshProgramPassesAsync(int tenantId, int? programId, CancellationToken ct = default)
        {
            var cards = await _dbContext.Set<LoyaltyCard>()
                .AsNoTracking()
                .Where(c => c.TenantId == tenantId
                         && c.IsActive
                         && c.Status == CardStatus.Active
                         && (c.IsAndroid || c.IsIOS)
                         && (programId == null || c.LoyaltyProgramId == programId))
                .Include(c => c.LoyaltyProgram).ThenInclude(p => p.CardTemplate).ThenInclude(t => t.Icons)
                .ToListAsync(ct);

            if (cards.Count == 0)
            {
                _logger.LogInformation("Suspension pass refresh: no wallet cards. TenantId={TenantId}", tenantId);
                return;
            }

            var tenantCompanyName = await _dbContext.Tenants
                .AsNoTracking()
                .Where(t => t.Id == tenantId)
                .Select(t => t.CompanyName)
                .FirstOrDefaultAsync(ct);

            var branding = await _dbContext.Set<TenantBranding>()
                .AsNoTracking()
                .FirstOrDefaultAsync(b => b.TenantId == tenantId, ct);

            var baseUrl = _config["App:BaseUrl"] ?? "https://appapi.walvero.com";
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

            var snapshotCache = new Dictionary<int, CardTemplateSnapshot>();
            int ok = 0, failed = 0;

            foreach (var card in cards)
            {
                ct.ThrowIfCancellationRequested();

                var program = card.LoyaltyProgram;
                if (program == null) { failed++; continue; }

                if (!snapshotCache.TryGetValue(card.LoyaltyProgramId, out var snapshot))
                {
                    snapshot = CardTemplateSnapshotFactory.From(
                        program, program.CardTemplate, branding, ToAbs);
                    snapshot.TenantCompanyName = tenantCompanyName;
                    snapshotCache[card.LoyaltyProgramId] = snapshot;

                    // Kart dizayneri: Google class-ı (başlıq/issuer/rəng/logo) proqram başına 1 dəfə yenilə —
                    // obyekt PATCH-ləri class-a toxunmur, dizayn dəyişikliyi yalnız belə çatır.
                    if (CardDesignFeatureFlags.GoogleClassUpdateEnabled
                        && cards.Any(c => c.LoyaltyProgramId == card.LoyaltyProgramId && c.IsAndroid))
                    {
                        try
                        {
                            var gw = await _googleFactory.CreateForTenantAsync(tenantId, card.LoyaltyProgramId, ct);
                            var cls = await gw.UpdateClassDesignAsync(snapshot, ct);
                            _logger.LogInformation("Refresh: Google class design update. ProgramId={ProgramId}, Success={Success}, Message={Message}",
                                card.LoyaltyProgramId, cls.Success, cls.Message);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Refresh: Google class design update failed. ProgramId={ProgramId}", card.LoyaltyProgramId);
                        }
                    }
                }

                // Ardıcıl işlənir — Google/Apple API-lərinə təbii rate limit.
                // Bir kartın xətası qalanları dayandırmır.
                if (card.IsAndroid)
                {
                    try
                    {
                        var res = await _walletRouter.RouteAsync(
                            WalletPlatform.GooglePay, card.Id, snapshot,
                            card.CurrentPoints, card.CurrentSteps, card.CompletedCycles, 0, ct);
                        if (res.Success) ok++; else { failed++; _logger.LogWarning("Suspension refresh Google failed. CardId={CardId}, Message={Message}", card.Id, res.Message); }
                    }
                    catch (Exception ex)
                    {
                        failed++;
                        _logger.LogWarning(ex, "Suspension refresh Google threw. CardId={CardId}", card.Id);
                    }
                }

                if (card.IsIOS)
                {
                    try
                    {
                        var res = await _walletRouter.RouteAsync(
                            WalletPlatform.AppleWallet, card.Id, snapshot,
                            card.CurrentPoints, card.CurrentSteps, card.CompletedCycles, 0, ct);
                        if (res.Success) ok++; else { failed++; _logger.LogWarning("Suspension refresh Apple failed. CardId={CardId}, Message={Message}", card.Id, res.Message); }
                    }
                    catch (Exception ex)
                    {
                        failed++;
                        _logger.LogWarning(ex, "Suspension refresh Apple threw. CardId={CardId}", card.Id);
                    }
                }
            }

            _logger.LogInformation(
                "Suspension pass refresh finished. TenantId={TenantId}, Cards={Cards}, Ok={Ok}, Failed={Failed}",
                tenantId, cards.Count, ok, failed);
        }
    }
}

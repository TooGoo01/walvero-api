using Hangfire;
using Loyalty.Business.Dtos.Post.Main;
using Loyalty.Business.Helpers;
using Loyalty.Business.Results;
using Loyalty.Business.Services.Abstractions.Main;
using Loyalty.DataAccess.Context;
using Loyalty.DataAccess.Entities.Main;
using Loyalty.DataAccess.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Loyalty.Business.Services.Implementations.Main
{
    /// <summary>
    /// Tenant admin bildirişləri v2 (2026-10-02): auditoriya filtrləri, dərhal / planlı göndəriş (eyni emal yolu — ScheduledNotification
    /// sətri, atomik claim), kvota (tenant yerli ayı; ad günü sayılmır), tarixçə, ad günü ayarları + job, müddətli ad günü bonusu.
    /// </summary>
    public sealed class TenantNotificationService : ITenantNotificationService, INotificationJobs
    {
        public const string BirthdayGiftDescription = "Ad günü hədiyyəsi";
        private const int SystemUserId = 1;
        private const int ScheduledBatch = 20;
        private static readonly TimeSpan MinScheduleLead = TimeSpan.FromMinutes(4);
        private static readonly TimeSpan MaxScheduleAhead = TimeSpan.FromDays(366);

        private readonly LoyaltyWalletDbContext _db;
        private readonly INotificationService _push;
        private readonly IWalletMessagingService _wallet;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<TenantNotificationService> _logger;

        public TenantNotificationService(
            LoyaltyWalletDbContext db,
            INotificationService push,
            IWalletMessagingService wallet,
            IServiceScopeFactory scopeFactory,
            ILogger<TenantNotificationService> logger)
        {
            _db = db;
            _push = push;
            _wallet = wallet;
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        private sealed record Target(int Id, int? UserId, string FirstName);
        private sealed record TenantInfo(int Id, string CompanyName, bool CanSend, int? Limit, TimeZoneInfo Tz);

        // =================== Tenant / vaxt ===================

        private async Task<TenantInfo?> LoadTenantAsync(int tenantId, CancellationToken ct)
        {
            var t = await _db.Tenants.AsNoTracking()
                .Where(x => x.Id == tenantId)
                .Select(x => new
                {
                    x.Id, x.CompanyName, x.IsActive, x.IsDeleted, x.IsServiceSuspended, x.MonthlyNotificationLimit, x.ReceiptTimeZoneId,
                    LocationTz = x.TenantLocation != null && x.TenantLocation.TimeZone != null ? x.TenantLocation.TimeZone.Name : null
                })
                .FirstOrDefaultAsync(ct);
            if (t == null) return null;
            return new TenantInfo(t.Id, t.CompanyName, t.IsActive && !t.IsDeleted && !t.IsServiceSuspended,
                t.MonthlyNotificationLimit, TenantClock.Resolve(t.LocationTz, t.ReceiptTimeZoneId));
        }

        private static string Iso(DateTime local) => local.ToString("yyyy-MM-ddTHH:mm:ss");

        // =================== Auditoriya ===================

        private static readonly TransactionType[] ActivityTypes = { TransactionType.Earned, TransactionType.Redeemed, TransactionType.Bonus };

        /// <summary>Filtrə uyğun aktiv müştərilər (yalnız bu tenant). Branch-scope rolları məcburi öz filialına.</summary>
        private IQueryable<Customer> BuildAudienceQuery(int tenantId, AudienceFilterDto f, int? branchScopeId, DateTime localToday)
        {
            var q = _db.Customers.AsNoTracking().Where(c => c.TenantId == tenantId && c.IsActive);

            if (f.CustomerId.HasValue)
            {
                var cid = f.CustomerId.Value;
                q = q.Where(c => c.Id == cid);
            }

            var branchId = branchScopeId ?? f.BranchId;
            if (branchId.HasValue)
            {
                var b = branchId.Value;
                // O filialda ən azı bir əməliyyatı olan müştərilər (köhnə qayda)
                q = q.Where(c => _db.LoyaltyCards.Any(lc => lc.CustomerId == c.Id &&
                    _db.Transactions.Any(t => t.LoyaltyCardId == lc.Id && t.BranchId == b)));
            }

            var maxPlausibleDob = localToday.AddYears(-BirthDateRules.MinPlausibleAge);
            var includeUnknown = f.IncludeUnknown;

            if (f.HasGenderFilter)
            {
                var genders = f.Genders!.Distinct().Select(g => (Gender)g).ToList();
                q = includeUnknown
                    ? q.Where(c => c.Gender == null || genders.Contains(c.Gender.Value))
                    : q.Where(c => c.Gender != null && genders.Contains(c.Gender.Value));
            }

            if (f.HasAgeFilter)
            {
                var maxDob = f.AgeMin.HasValue ? BirthDateRules.MaxDobForMinAge(localToday, f.AgeMin.Value) : maxPlausibleDob;
                if (maxDob > maxPlausibleDob) maxDob = maxPlausibleDob;
                var minDobExcl = f.AgeMax.HasValue ? BirthDateRules.MinDobExclusiveForMaxAge(localToday, f.AgeMax.Value) : DateTime.MinValue;
                // "Yararlı DOB" = yaş ≥ 6 və qeydiyyat günü deyil (BirthDateRules.IsUsable ilə eyni)
                q = includeUnknown
                    ? q.Where(c =>
                        (c.DateOfBirth != null && c.DateOfBirth <= maxDob && c.DateOfBirth > minDobExcl && c.DateOfBirth.Value.Date != c.RegDate.Date)
                        || c.DateOfBirth == null || c.DateOfBirth > maxPlausibleDob || c.DateOfBirth.Value.Date == c.RegDate.Date)
                    : q.Where(c =>
                        c.DateOfBirth != null && c.DateOfBirth <= maxDob && c.DateOfBirth > minDobExcl && c.DateOfBirth.Value.Date != c.RegDate.Date);
            }

            if (f.ActiveWithinDays.HasValue || f.InactiveForDays.HasValue)
            {
                var days = f.ActiveWithinDays ?? f.InactiveForDays!.Value;
                var cutoff = DateTime.UtcNow.AddDays(-days);
                var types = ActivityTypes;
                if (f.ActiveWithinDays.HasValue)
                    q = q.Where(c => _db.LoyaltyCards.Any(lc => lc.CustomerId == c.Id &&
                        _db.Transactions.Any(t => t.LoyaltyCardId == lc.Id && t.IsActive && t.RegDate >= cutoff
                            && (t.Status == RedeemStatus.Applied || t.Status == RedeemStatus.Pending) && types.Contains(t.Type))));
                else
                    q = q.Where(c => c.RegDate <= cutoff && !_db.LoyaltyCards.Any(lc => lc.CustomerId == c.Id &&
                        _db.Transactions.Any(t => t.LoyaltyCardId == lc.Id && t.IsActive && t.RegDate >= cutoff
                            && (t.Status == RedeemStatus.Applied || t.Status == RedeemStatus.Pending) && types.Contains(t.Type))));
            }

            if (f.MinBalance.HasValue)
            {
                var min = f.MinBalance.Value;
                q = q.Where(c => _db.LoyaltyCards.Any(lc => lc.CustomerId == c.Id && lc.TenantId == tenantId && lc.IsActive && lc.CurrentPoints >= min));
            }
            if (f.MaxBalance.HasValue)
            {
                var max = f.MaxBalance.Value;
                q = q.Where(c => _db.LoyaltyCards.Any(lc => lc.CustomerId == c.Id && lc.TenantId == tenantId && lc.IsActive && lc.CurrentPoints <= max));
            }
            if (f.HasReadyGift == true)
                q = q.Where(c => _db.LoyaltyCards.Any(lc => lc.CustomerId == c.Id && lc.TenantId == tenantId && lc.IsActive && lc.CompletedCycles > 0));

            if (f.EffectiveChannel == "app")
                q = q.Where(c => c.applicationUserId != null);
            else if (f.EffectiveChannel == "wallet")
                q = q.Where(c => _db.LoyaltyCards.Any(lc => lc.CustomerId == c.Id && lc.TenantId == tenantId && (lc.IsIOS || lc.IsAndroid)));

            return q;
        }

        public async Task<ServiceResult> PreviewAudienceAsync(TenantNotificationContext ctx, AudienceFilterDto filter, CancellationToken ct = default)
        {
            var err = filter.Validate();
            if (err != null) return new ServiceResult(false, err);
            var tenant = await LoadTenantAsync(ctx.TenantId, ct);
            if (tenant == null) return new ServiceResult(false, "Tenant tapılmadı.");

            var today = TenantClock.LocalNow(tenant.Tz).Date;
            var maxPlausibleDob = today.AddYears(-BirthDateRules.MinPlausibleAge);
            var q = BuildAudienceQuery(ctx.TenantId, filter, ctx.BranchScopeId, today);
            var tenantId = ctx.TenantId;

            var total = await q.CountAsync(ct);
            var app = await q.CountAsync(c => c.applicationUserId != null, ct);
            var wallet = await q.CountAsync(c => _db.LoyaltyCards.Any(lc => lc.CustomerId == c.Id && lc.TenantId == tenantId && (lc.IsIOS || lc.IsAndroid)), ct);
            var unknownGender = await q.CountAsync(c => c.Gender == null, ct);
            var unknownAge = await q.CountAsync(c => c.DateOfBirth == null || c.DateOfBirth > maxPlausibleDob || c.DateOfBirth.Value.Date == c.RegDate.Date, ct);

            return new ServiceResult(true, new { total, appReachable = app, walletReachable = wallet, unknownGender, unknownAge });
        }

        // =================== Kvota ===================

        private async Task<int> CountUsedThisMonthAsync(int tenantId, TimeZoneInfo tz, int? excludeScheduledId, CancellationToken ct)
        {
            var (start, end) = TenantClock.MonthRangeUtc(tz);
            var sent = await _db.TenantNotificationLogs.AsNoTracking()
                .CountAsync(l => l.TenantId == tenantId && l.RegDate >= start && l.RegDate < end
                                 && l.Source != TenantNotificationLog.SourceBirthday, ct);
            // Planlı (hələ göndərilməyən) göndərişlər həmin ayın kvotasında yer tutur
            var pending = await _db.ScheduledNotifications.AsNoTracking()
                .CountAsync(s => s.TenantId == tenantId && s.IsActive
                                 && (s.Status == ScheduledNotificationStatus.Pending || s.Status == ScheduledNotificationStatus.Sending)
                                 && s.ScheduledAtUtc >= start && s.ScheduledAtUtc < end
                                 && (excludeScheduledId == null || s.Id != excludeScheduledId), ct);
            return sent + pending;
        }

        public async Task<ServiceResult> GetQuotaAsync(int tenantId, CancellationToken ct = default)
        {
            var tenant = await LoadTenantAsync(tenantId, ct);
            if (tenant == null) return new ServiceResult(false, "Tenant tapılmadı.");
            var used = await CountUsedThisMonthAsync(tenantId, tenant.Tz, null, ct);
            return new ServiceResult(true, new NotificationQuotaDto(
                tenant.Limit, used, tenant.Limit.HasValue ? Math.Max(0, tenant.Limit.Value - used) : null));
        }

        // =================== Göndər / planlaşdır ===================

        private static AudienceFilterDto MergeAudience(SendTenantNotificationRequest r)
        {
            var f = r.Audience ?? new AudienceFilterDto();
            f.BranchId ??= r.BranchId;
            f.CustomerId ??= r.CustomerId;
            return f;
        }

        private static string? ValidateMessage(SendTenantNotificationRequest r)
        {
            if (!r.ToApp && !r.ToWallet) return "Ən azı bir kanal (App/Wallet) seçilməlidir.";
            if (string.IsNullOrWhiteSpace(r.Title) || string.IsNullOrWhiteSpace(r.Body)) return "Başlıq və mətn boş ola bilməz.";
            if (r.Title.Trim().Length > 200) return "Başlıq maksimum 200 simvol ola bilər.";
            if (r.Body.Trim().Length > 1000) return "Mətn maksimum 1000 simvol ola bilər.";
            return null;
        }

        private async Task<string?> ValidateTargetsAsync(TenantNotificationContext ctx, AudienceFilterDto f, CancellationToken ct)
        {
            if (f.CustomerId.HasValue &&
                !await _db.Customers.AsNoTracking().AnyAsync(c => c.Id == f.CustomerId.Value && c.TenantId == ctx.TenantId && c.IsActive, ct))
                return "Müştəri tapılmadı.";
            if (f.BranchId.HasValue &&
                !await _db.Branches.AsNoTracking().AnyAsync(b => b.Id == f.BranchId.Value && b.TenantId == ctx.TenantId, ct))
                return "Filial tapılmadı.";
            if (ctx.BranchScopeId.HasValue && f.BranchId.HasValue && f.BranchId != ctx.BranchScopeId)
                return "Yalnız öz filialınıza göndərə bilərsiniz.";
            return null;
        }

        public async Task<ServiceResult> SendOrScheduleAsync(TenantNotificationContext ctx, SendTenantNotificationRequest request, CancellationToken ct = default)
        {
            var msgErr = ValidateMessage(request);
            if (msgErr != null) return new ServiceResult(false, msgErr);
            var filter = MergeAudience(request);
            var err = filter.Validate() ?? await ValidateTargetsAsync(ctx, filter, ct);
            if (err != null) return new ServiceResult(false, err);

            var tenant = await LoadTenantAsync(ctx.TenantId, ct);
            if (tenant == null) return new ServiceResult(false, "Tenant tapılmadı.");
            if (!tenant.CanSend) return new ServiceResult(false, "Tenant aktiv deyil — bildiriş göndərmək mümkün deyil.");

            var nowUtc = DateTime.UtcNow;
            DateTime scheduledUtc = nowUtc;
            var immediate = request.ScheduledAtLocal == null;
            if (!immediate)
            {
                scheduledUtc = TenantClock.ToUtc(request.ScheduledAtLocal!.Value, tenant.Tz);
                if (scheduledUtc < nowUtc + MinScheduleLead) return new ServiceResult(false, "Planlı vaxt ən azı 5 dəqiqə sonra olmalıdır.");
                if (scheduledUtc > nowUtc + MaxScheduleAhead) return new ServiceResult(false, "Ən çox 1 il qabağa planlaşdırmaq olar.");
            }

            // Kvota (planlı göndəriş öz ayının kvotasında yer tutur)
            if (tenant.Limit.HasValue)
            {
                var tz = tenant.Tz;
                var used = immediate || SameLocalMonth(scheduledUtc, nowUtc, tz)
                    ? await CountUsedThisMonthAsync(ctx.TenantId, tz, null, ct)
                    : await CountUsedInMonthAsync(ctx.TenantId, tz, scheduledUtc, null, ct);
                if (used >= tenant.Limit.Value)
                    return new ServiceResult(false, $"Aylıq bildiriş limitiniz bitib ({used}/{tenant.Limit}).");
            }

            var row = new ScheduledNotification
            {
                TenantId = ctx.TenantId,
                Title = request.Title.Trim(),
                Body = request.Body.Trim(),
                AudienceJson = filter.ToJson(),
                ToApp = request.ToApp,
                ToWallet = request.ToWallet,
                ScheduledAtUtc = scheduledUtc,
                Status = ScheduledNotificationStatus.Pending,
                BranchScopeId = ctx.BranchScopeId,
                IsImmediate = immediate,
                RegUserId = ctx.UserId,
                RegDate = nowUtc,
                IsActive = true
            };
            _db.ScheduledNotifications.Add(row);
            await _db.SaveChangesAsync(ct);

            if (!immediate)
            {
                var local = TenantClock.ToLocal(scheduledUtc, tenant.Tz);
                return new ServiceResult(true, new { scheduled = true, id = row.Id, scheduledAtLocal = Iso(local) },
                    $"Bildiriş {local:dd.MM.yyyy HH:mm}-də göndəriləcək.");
            }

            // İndi göndər — planlı ilə eyni emal yolu (claim → auditoriya → göndəriş → log)
            var outcome = await ProcessScheduledAsync(row.Id, ct);
            if (outcome == null) return new ServiceResult(false, "Göndəriş başlamadı.");
            if (!outcome.Value.Ok) return new ServiceResult(false, outcome.Value.Error ?? "Göndəriş alınmadı.");
            var quota = await GetQuotaAsync(ctx.TenantId, ct);
            return new ServiceResult(true, new
            {
                scheduled = false,
                recipientCount = outcome.Value.Recipients,
                appDelivered = outcome.Value.App,
                walletDelivered = outcome.Value.Wallet,
                quota = quota.Data
            }, $"{outcome.Value.Recipients} müştəri · App: {outcome.Value.App} · Wallet: {outcome.Value.Wallet}");
        }

        private static bool SameLocalMonth(DateTime aUtc, DateTime bUtc, TimeZoneInfo tz)
        {
            var a = TenantClock.ToLocal(aUtc, tz); var b = TenantClock.ToLocal(bUtc, tz);
            return a.Year == b.Year && a.Month == b.Month;
        }

        private async Task<int> CountUsedInMonthAsync(int tenantId, TimeZoneInfo tz, DateTime anyUtcInMonth, int? excludeScheduledId, CancellationToken ct)
        {
            var (start, end) = TenantClock.MonthRangeUtc(tz, anyUtcInMonth);
            var sent = await _db.TenantNotificationLogs.AsNoTracking()
                .CountAsync(l => l.TenantId == tenantId && l.RegDate >= start && l.RegDate < end && l.Source != TenantNotificationLog.SourceBirthday, ct);
            var pending = await _db.ScheduledNotifications.AsNoTracking()
                .CountAsync(s => s.TenantId == tenantId && s.IsActive
                                 && (s.Status == ScheduledNotificationStatus.Pending || s.Status == ScheduledNotificationStatus.Sending)
                                 && s.ScheduledAtUtc >= start && s.ScheduledAtUtc < end
                                 && (excludeScheduledId == null || s.Id != excludeScheduledId), ct);
            return sent + pending;
        }

        // =================== Emal (job + dərhal) ===================

        private readonly record struct Outcome(bool Ok, int Recipients, int App, int Wallet, string? Error);

        /// <summary>Pending sətri atomik götür (Pending→Sending) və göndər. null = artıq başqası götürüb.</summary>
        private async Task<Outcome?> ProcessScheduledAsync(int id, CancellationToken ct)
        {
            var nowUtc = DateTime.UtcNow;
            var claimed = await _db.ScheduledNotifications
                .Where(s => s.Id == id && s.Status == ScheduledNotificationStatus.Pending)
                .ExecuteUpdateAsync(set => set
                    .SetProperty(s => s.Status, ScheduledNotificationStatus.Sending)
                    .SetProperty(s => s.EditDate, nowUtc), ct);
            if (claimed == 0) return null;

            var row = await _db.ScheduledNotifications.FirstAsync(s => s.Id == id, ct);
            try
            {
                var tenant = await LoadTenantAsync(row.TenantId, ct);
                if (tenant == null || !tenant.CanSend)
                    return await FailAsync(row, "Tenant aktiv deyil.", ct);

                var filter = AudienceFilterDto.Parse(row.AudienceJson);
                var today = TenantClock.LocalNow(tenant.Tz).Date;
                var targets = await BuildAudienceQuery(row.TenantId, filter, row.BranchScopeId, today)
                    .Select(c => new Target(c.Id, c.applicationUserId, c.FirstName))
                    .ToListAsync(ct);

                var (app, wallet) = await DeliverAsync(targets, row.Title, row.Body, row.ToApp, row.ToWallet, tenant.CompanyName, _ => null, ct);

                string? branchName = filter.BranchId.HasValue
                    ? await _db.Branches.AsNoTracking().Where(b => b.Id == filter.BranchId).Select(b => b.Name).FirstOrDefaultAsync(ct) : null;
                string? customerName = filter.CustomerId.HasValue
                    ? await _db.Customers.AsNoTracking().Where(c => c.Id == filter.CustomerId).Select(c => (c.FirstName + " " + c.LastName).Trim()).FirstOrDefaultAsync(ct) : null;

                _db.TenantNotificationLogs.Add(new TenantNotificationLog
                {
                    TenantId = row.TenantId,
                    Scope = filter.CustomerId.HasValue ? "customer" : filter.BranchId.HasValue ? "branch" : IsUnfiltered(filter) ? "tenant" : "filtered",
                    BranchId = filter.BranchId,
                    CustomerId = filter.CustomerId,
                    Title = Truncate(row.Title, 200),
                    Body = row.Body,
                    RecipientCount = targets.Count,
                    Source = row.IsImmediate ? TenantNotificationLog.SourceManual : TenantNotificationLog.SourceScheduled,
                    Channels = Channels(row.ToApp, row.ToWallet),
                    ScheduledNotificationId = row.Id,
                    AudienceSummary = Truncate(filter.Summary(branchName, customerName), 500),
                    AppSent = app,
                    WalletSent = wallet,
                    RegUserId = row.RegUserId,
                    RegDate = DateTime.UtcNow,
                    IsActive = true
                });
                row.Status = ScheduledNotificationStatus.Sent;
                row.SentAtUtc = DateTime.UtcNow;
                row.RecipientCount = targets.Count;
                row.EditDate = DateTime.UtcNow;
                await _db.SaveChangesAsync(ct);

                _logger.LogInformation("Tenant notification sent. Id={Id} TenantId={TenantId} Recipients={Count} App={App} Wallet={Wallet} Immediate={Immediate}",
                    row.Id, row.TenantId, targets.Count, app, wallet, row.IsImmediate);
                return new Outcome(true, targets.Count, app, wallet, null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Tenant notification failed. Id={Id} TenantId={TenantId}", row.Id, row.TenantId);
                return await FailAsync(row, ex.Message, ct);
            }
        }

        private async Task<Outcome> FailAsync(ScheduledNotification row, string error, CancellationToken ct)
        {
            row.Status = ScheduledNotificationStatus.Failed;
            row.Error = Truncate(error, 500);
            row.EditDate = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
            return new Outcome(false, 0, 0, 0, error);
        }

        private static bool IsUnfiltered(AudienceFilterDto f) =>
            !f.HasGenderFilter && !f.HasAgeFilter && !f.ActiveWithinDays.HasValue && !f.InactiveForDays.HasValue
            && !f.MinBalance.HasValue && !f.MaxBalance.HasValue && f.HasReadyGift != true && f.EffectiveChannel == "all";

        private static string Channels(bool app, bool wallet) => app && wallet ? "app,wallet" : app ? "app" : "wallet";
        private static string Truncate(string s, int max) => s.Length <= max ? s : s[..max];

        /// <summary>App (Notification sətri + FCM) və wallet (Apple/Google) çatdırılması; {ad}/{restoran}/{hədiyyə} personalizasiyası.</summary>
        private async Task<(int App, int Wallet)> DeliverAsync(
            List<Target> targets, string title, string body, bool toApp, bool toWallet, string tenantName,
            Func<Target, string?> giftText, CancellationToken ct)
        {
            int app = 0, wallet = 0;
            if (targets.Count == 0) return (0, 0);

            if (toApp)
            {
                var appTargets = targets.Where(t => t.UserId.HasValue).GroupBy(t => t.UserId!.Value).Select(g => g.First()).ToList();
                if (appTargets.Count > 0)
                {
                    var now = DateTime.UtcNow;
                    var rows = appTargets.Select(t => new Notification
                    {
                        UserId = t.UserId!.Value,
                        Title = NotificationTemplate.Render(title, t.FirstName, tenantName, giftText(t)),
                        Body = NotificationTemplate.Render(body, t.FirstName, tenantName, giftText(t)),
                        IsRead = false,
                        CreatedAt = now,
                        RegUserId = SystemUserId,
                        RegDate = now,
                        IsActive = true
                    }).ToList();
                    await _db.Notifications.AddRangeAsync(rows, ct);
                    await _db.SaveChangesAsync(ct);

                    foreach (var t in appTargets)
                    {
                        try
                        {
                            await _push.SendToUserAsync(t.UserId!.Value,
                                NotificationTemplate.Render(title, t.FirstName, tenantName, giftText(t)),
                                NotificationTemplate.Render(body, t.FirstName, tenantName, giftText(t)));
                        }
                        catch (Exception ex) { _logger.LogWarning(ex, "FCM push failed. UserId={UserId}", t.UserId); }
                    }
                    app = appTargets.Count;
                }
            }

            if (toWallet)
            {
                foreach (var t in targets)
                {
                    try
                    {
                        var res = await _wallet.SendToCustomerAsync(t.Id,
                            NotificationTemplate.Render(title, t.FirstName, tenantName, giftText(t)),
                            NotificationTemplate.Render(body, t.FirstName, tenantName, giftText(t)), ct);
                        wallet += res.Data is int n ? n : 0;
                    }
                    catch (Exception ex) { _logger.LogWarning(ex, "Wallet message failed. CustomerId={CustomerId}", t.Id); }
                }
            }
            return (app, wallet);
        }

        // =================== Planlı siyahı / redaktə / ləğv / tarixçə ===================

        public async Task<ServiceResult> ListScheduledAsync(TenantNotificationContext ctx, CancellationToken ct = default)
        {
            var tenant = await LoadTenantAsync(ctx.TenantId, ct);
            if (tenant == null) return new ServiceResult(false, "Tenant tapılmadı.");
            var since = DateTime.UtcNow.AddDays(-60);
            var rows = await _db.ScheduledNotifications.AsNoTracking()
                .Where(s => s.TenantId == ctx.TenantId && s.IsActive && !s.IsImmediate
                            && (ctx.BranchScopeId == null || s.BranchScopeId == ctx.BranchScopeId)
                            && (s.Status == ScheduledNotificationStatus.Pending || s.Status == ScheduledNotificationStatus.Sending || s.ScheduledAtUtc >= since))
                .OrderBy(s => s.ScheduledAtUtc)
                .Take(300)
                .ToListAsync(ct);

            var items = rows.Select(s =>
            {
                var f = AudienceFilterDto.Parse(s.AudienceJson);
                return new
                {
                    s.Id, s.Title, s.Body, s.ToApp, s.ToWallet,
                    scheduledAtLocal = Iso(TenantClock.ToLocal(s.ScheduledAtUtc, tenant.Tz)),
                    status = s.Status.ToString(),
                    sentAtLocal = s.SentAtUtc.HasValue ? Iso(TenantClock.ToLocal(s.SentAtUtc.Value, tenant.Tz)) : null,
                    s.RecipientCount, s.Error,
                    audience = f,
                    audienceSummary = f.Summary()
                };
            }).ToList();
            return new ServiceResult(true, items);
        }

        public async Task<ServiceResult> UpdateScheduledAsync(TenantNotificationContext ctx, int id, SendTenantNotificationRequest request, CancellationToken ct = default)
        {
            var row = await _db.ScheduledNotifications.FirstOrDefaultAsync(s => s.Id == id && s.TenantId == ctx.TenantId && s.IsActive, ct);
            if (row == null || (ctx.BranchScopeId.HasValue && row.BranchScopeId != ctx.BranchScopeId))
                return new ServiceResult(false, "Planlı bildiriş tapılmadı.");
            if (row.Status != ScheduledNotificationStatus.Pending) return new ServiceResult(false, "Yalnız gözləyən bildirişi dəyişmək olar.");
            if (request.ScheduledAtLocal == null) return new ServiceResult(false, "Tarix seçilməlidir.");

            var msgErr = ValidateMessage(request);
            if (msgErr != null) return new ServiceResult(false, msgErr);
            var filter = MergeAudience(request);
            var err = filter.Validate() ?? await ValidateTargetsAsync(ctx, filter, ct);
            if (err != null) return new ServiceResult(false, err);

            var tenant = await LoadTenantAsync(ctx.TenantId, ct);
            if (tenant == null) return new ServiceResult(false, "Tenant tapılmadı.");
            var nowUtc = DateTime.UtcNow;
            if (row.ScheduledAtUtc <= nowUtc.AddMinutes(1)) return new ServiceResult(false, "Göndəriş vaxtı çatıb — artıq dəyişmək olmaz.");
            var scheduledUtc = TenantClock.ToUtc(request.ScheduledAtLocal.Value, tenant.Tz);
            if (scheduledUtc < nowUtc + MinScheduleLead) return new ServiceResult(false, "Planlı vaxt ən azı 5 dəqiqə sonra olmalıdır.");
            if (scheduledUtc > nowUtc + MaxScheduleAhead) return new ServiceResult(false, "Ən çox 1 il qabağa planlaşdırmaq olar.");

            if (tenant.Limit.HasValue && !SameLocalMonth(scheduledUtc, row.ScheduledAtUtc, tenant.Tz))
            {
                var used = await CountUsedInMonthAsync(ctx.TenantId, tenant.Tz, scheduledUtc, row.Id, ct);
                if (used >= tenant.Limit.Value) return new ServiceResult(false, $"Seçilmiş ayın bildiriş limiti doludur ({used}/{tenant.Limit}).");
            }

            row.Title = request.Title.Trim();
            row.Body = request.Body.Trim();
            row.AudienceJson = filter.ToJson();
            row.ToApp = request.ToApp;
            row.ToWallet = request.ToWallet;
            row.ScheduledAtUtc = scheduledUtc;
            row.EditDate = nowUtc;
            row.EditUserId = ctx.UserId;
            await _db.SaveChangesAsync(ct);
            return new ServiceResult(true, new { id = row.Id, scheduledAtLocal = Iso(TenantClock.ToLocal(scheduledUtc, tenant.Tz)) }, "Planlı bildiriş yeniləndi.");
        }

        public async Task<ServiceResult> CancelScheduledAsync(TenantNotificationContext ctx, int id, CancellationToken ct = default)
        {
            var nowUtc = DateTime.UtcNow;
            var updated = await _db.ScheduledNotifications
                .Where(s => s.Id == id && s.TenantId == ctx.TenantId && s.IsActive && s.Status == ScheduledNotificationStatus.Pending
                            && (ctx.BranchScopeId == null || s.BranchScopeId == ctx.BranchScopeId))
                .ExecuteUpdateAsync(set => set
                    .SetProperty(s => s.Status, ScheduledNotificationStatus.Cancelled)
                    .SetProperty(s => s.EditDate, nowUtc)
                    .SetProperty(s => s.EditUserId, ctx.UserId), ct);
            return updated == 0
                ? new ServiceResult(false, "Ləğv etmək mümkün olmadı (artıq göndərilib və ya tapılmadı).")
                : new ServiceResult(true, "Planlı bildiriş ləğv edildi.");
        }

        public async Task<ServiceResult> HistoryAsync(TenantNotificationContext ctx, int page, int pageSize, CancellationToken ct = default)
        {
            var tenant = await LoadTenantAsync(ctx.TenantId, ct);
            if (tenant == null) return new ServiceResult(false, "Tenant tapılmadı.");
            page = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 5, 100);
            var q = _db.TenantNotificationLogs.AsNoTracking().Where(l => l.TenantId == ctx.TenantId);
            if (ctx.BranchScopeId.HasValue) q = q.Where(l => l.BranchId == ctx.BranchScopeId);
            var total = await q.CountAsync(ct);
            var rows = await q.OrderByDescending(l => l.RegDate).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
            var items = rows.Select(l => new
            {
                l.Id, l.Title, l.Body, l.Scope, l.Source, l.Channels, l.RecipientCount, l.AppSent, l.WalletSent, l.AudienceSummary,
                sentAtLocal = Iso(TenantClock.ToLocal(l.RegDate, tenant.Tz))
            }).ToList();
            return new ServiceResult(true, new { items, total, page, pageSize });
        }

        // =================== Ad günü ayarları ===================

        private const string DefaultBirthdayTitle = "Ad gününüz mübarək, {ad}!";
        private const string DefaultBirthdayBody = "{restoran} komandası sizi ad gününüz münasibətilə təbrik edir. Sizə {hədiyyə} hədiyyə etdik — növbəti ziyarətinizdə istifadə edin!";
        private const string DefaultBirthdayBodyNoGift = "{restoran} komandası sizi ad gününüz münasibətilə təbrik edir. Sizi gözləyirik!";

        private async Task<(bool HasStamp, bool HasPoints)> ProgramKindsAsync(int tenantId, CancellationToken ct)
        {
            var templates = await _db.LoyaltyPrograms.AsNoTracking()
                .Where(p => p.TenantId == tenantId && p.CardTemplate != null)
                .Select(p => p.CardTemplate!)
                .ToListAsync(ct);
            var hasStamp = templates.Any(t => t.EffectiveType == CardTemplateType.ProgressBased);
            var hasPoints = templates.Any(t => t.EffectiveType != CardTemplateType.ProgressBased);
            return (hasStamp, hasPoints);
        }

        public async Task<ServiceResult> GetBirthdaySettingsAsync(int tenantId, CancellationToken ct = default)
        {
            var s = await _db.TenantBirthdaySettings.AsNoTracking().FirstOrDefaultAsync(x => x.TenantId == tenantId, ct);
            var (hasStamp, hasPoints) = await ProgramKindsAsync(tenantId, ct);
            var dto = s == null
                ? new BirthdaySettingsDto { Enabled = false, SendHourLocal = 10, Title = DefaultBirthdayTitle, Body = DefaultBirthdayBodyNoGift, ToApp = true, ToWallet = true }
                : new BirthdaySettingsDto
                {
                    Enabled = s.Enabled, SendHourLocal = s.SendHourLocal, Title = s.Title, Body = s.Body, ToApp = s.ToApp, ToWallet = s.ToWallet,
                    GiftEnabled = s.GiftEnabled, GiftValue = s.GiftValue, GiftExpiryDays = s.GiftExpiryDays
                };
            return new ServiceResult(true, new
            {
                settings = dto,
                exists = s != null,
                hasStampProgram = hasStamp,
                hasPointsProgram = hasPoints,
                defaultBodyWithGift = DefaultBirthdayBody,
                defaultBodyNoGift = DefaultBirthdayBodyNoGift,
                lastRunLocalDate = s?.LastRunLocalDate?.ToString("yyyy-MM-dd")
            });
        }

        public async Task<ServiceResult> SaveBirthdaySettingsAsync(TenantNotificationContext ctx, BirthdaySettingsDto dto, CancellationToken ct = default)
        {
            if (ctx.BranchScopeId.HasValue) return new ServiceResult(false, "Ad günü ayarlarını yalnız tenant admin dəyişə bilər.");
            if (dto.SendHourLocal is < 0 or > 23) return new ServiceResult(false, "Göndərmə saatı 0–23 olmalıdır.");
            if (dto.Enabled && (string.IsNullOrWhiteSpace(dto.Title) || string.IsNullOrWhiteSpace(dto.Body)))
                return new ServiceResult(false, "Başlıq və mətn boş ola bilməz.");
            if (dto.Enabled && !dto.ToApp && !dto.ToWallet) return new ServiceResult(false, "Ən azı bir kanal seçilməlidir.");
            if ((dto.Title?.Length ?? 0) > 200 || (dto.Body?.Length ?? 0) > 1000) return new ServiceResult(false, "Başlıq ≤ 200, mətn ≤ 1000 simvol.");
            if (dto.GiftEnabled && (dto.GiftValue <= 0 || dto.GiftValue > 1000)) return new ServiceResult(false, "Hədiyyə dəyəri 0-dan böyük, ən çox 1000 olmalıdır.");
            if (dto.GiftExpiryDays is <= 0 or > 365) return new ServiceResult(false, "Hədiyyənin müddəti 1–365 gün olmalıdır.");

            var now = DateTime.UtcNow;
            var s = await _db.TenantBirthdaySettings.FirstOrDefaultAsync(x => x.TenantId == ctx.TenantId, ct);
            if (s == null)
            {
                s = new TenantBirthdaySetting { TenantId = ctx.TenantId, RegUserId = ctx.UserId, RegDate = now, IsActive = true };
                _db.TenantBirthdaySettings.Add(s);
            }
            s.Enabled = dto.Enabled;
            s.SendHourLocal = dto.SendHourLocal;
            s.Title = (dto.Title ?? "").Trim();
            s.Body = (dto.Body ?? "").Trim();
            s.ToApp = dto.ToApp;
            s.ToWallet = dto.ToWallet;
            s.GiftEnabled = dto.GiftEnabled;
            s.GiftValue = dto.GiftEnabled ? dto.GiftValue : 0;
            s.GiftExpiryDays = dto.GiftEnabled ? dto.GiftExpiryDays : null;
            s.EditDate = now;
            s.EditUserId = ctx.UserId;
            await _db.SaveChangesAsync(ct);
            return await GetBirthdaySettingsAsync(ctx.TenantId, ct);
        }

        public async Task<ServiceResult> UpcomingBirthdaysAsync(int tenantId, int days, CancellationToken ct = default)
        {
            var tenant = await LoadTenantAsync(tenantId, ct);
            if (tenant == null) return new ServiceResult(false, "Tenant tapılmadı.");
            days = Math.Clamp(days, 1, 31);
            var today = TenantClock.LocalNow(tenant.Tz).Date;

            var all = await _db.Customers.AsNoTracking()
                .Where(c => c.TenantId == tenantId && c.IsActive)
                .Select(c => new { c.Id, c.FirstName, c.LastName, c.PhoneNumber, c.DateOfBirth, c.RegDate })
                .ToListAsync(ct);
            var usable = all.Where(c => BirthDateRules.IsUsable(c.DateOfBirth, c.RegDate, today)).ToList();

            var upcoming = usable
                .Select(c => new { c, next = BirthDateRules.NextBirthday(c.DateOfBirth!.Value, today) })
                .Where(x => (x.next - today).TotalDays < days)
                .OrderBy(x => x.next).ThenBy(x => x.c.FirstName)
                .Take(200)
                .Select(x => new
                {
                    customerId = x.c.Id,
                    name = $"{x.c.FirstName} {x.c.LastName}".Trim(),
                    phone = x.c.PhoneNumber,
                    date = x.next.ToString("yyyy-MM-dd"),
                    turnsAge = BirthDateRules.Age(x.c.DateOfBirth!.Value, x.next),
                    isToday = x.next == today
                })
                .ToList();

            var sentThisYear = await _db.BirthdayNotificationLogs.AsNoTracking()
                .CountAsync(l => l.TenantId == tenantId && l.Year == today.Year, ct);

            return new ServiceResult(true, new
            {
                today = today.ToString("yyyy-MM-dd"),
                days,
                count = upcoming.Count,
                items = upcoming,
                totalCustomers = all.Count,
                withBirthDate = usable.Count,
                coveragePercent = all.Count == 0 ? 0 : (int)Math.Round(usable.Count * 100.0 / all.Count),
                sentThisYear
            });
        }

        // =================== Job-lar ===================

        [AutomaticRetry(Attempts = 0)]
        [Queue("default")]
        public async Task RunScheduledAsync(int? onlyTenantId, CancellationToken ct = default)
        {
            var now = DateTime.UtcNow;
            var due = await _db.ScheduledNotifications.AsNoTracking()
                .Where(s => s.Status == ScheduledNotificationStatus.Pending && s.IsActive && s.ScheduledAtUtc <= now
                            && (onlyTenantId == null || s.TenantId == onlyTenantId))
                .OrderBy(s => s.ScheduledAtUtc)
                .Select(s => s.Id)
                .Take(ScheduledBatch)
                .ToListAsync(ct);
            foreach (var id in due)
            {
                try { await ProcessScheduledAsync(id, ct); }
                catch (Exception ex) { _logger.LogError(ex, "Scheduled notification job item failed. Id={Id}", id); }
                _db.ChangeTracker.Clear();
            }
        }

        [AutomaticRetry(Attempts = 0)]
        [Queue("default")]
        public async Task RunBirthdaysAsync(int? onlyTenantId, bool ignoreSendHour, CancellationToken ct = default)
        {
            var settings = await _db.TenantBirthdaySettings.AsNoTracking()
                .Where(s => s.Enabled && s.IsActive && (onlyTenantId == null || s.TenantId == onlyTenantId))
                .ToListAsync(ct);
            foreach (var s in settings)
            {
                try { await ProcessTenantBirthdaysAsync(s, ignoreSendHour, ct); }
                catch (Exception ex) { _logger.LogError(ex, "Birthday job failed. TenantId={TenantId}", s.TenantId); }
                _db.ChangeTracker.Clear();
            }
        }

        public async Task<string> RunBirthdayForCustomerAsync(int tenantId, int customerId, CancellationToken ct = default)
        {
            var s = await _db.TenantBirthdaySettings.AsNoTracking().FirstOrDefaultAsync(x => x.TenantId == tenantId, ct);
            if (s == null) return "Ad günü ayarı yoxdur — əvvəl saxlayın (söndürülü də olar).";
            var c = await _db.Customers.AsNoTracking()
                .Where(x => x.Id == customerId && x.TenantId == tenantId && x.IsActive)
                .Select(x => new { x.Id, x.applicationUserId, x.FirstName })
                .FirstOrDefaultAsync(ct);
            if (c == null) return "Müştəri tapılmadı.";
            var tenant = await LoadTenantAsync(tenantId, ct);
            if (tenant == null || !tenant.CanSend) return "Tenant aktiv deyil.";
            var today = TenantClock.LocalNow(tenant.Tz).Date;
            if (await _db.BirthdayNotificationLogs.AsNoTracking().AnyAsync(l => l.TenantId == tenantId && l.CustomerId == c.Id && l.Year == today.Year, ct))
                return "Bu müştəri bu il artıq təbrik olunub (idempotentlik).";

            var log = new BirthdayNotificationLog
            {
                TenantId = tenantId, CustomerId = c.Id, Year = today.Year, SentAtUtc = DateTime.UtcNow,
                RegUserId = SystemUserId, RegDate = DateTime.UtcNow, IsActive = true
            };
            _db.BirthdayNotificationLogs.Add(log);
            try { await _db.SaveChangesAsync(ct); }
            catch (DbUpdateException) { return "Bu müştəri bu il artıq təbrik olunub (idempotentlik)."; }

            string? giftText = null;
            if (s.GiftEnabled && s.GiftValue > 0)
                giftText = await GrantBirthdayGiftAsync(s, c.Id, log, ct);
            var gt = giftText;
            var (app, wallet) = await DeliverAsync(new List<Target> { new(c.Id, c.applicationUserId, c.FirstName) },
                s.Title, s.Body, s.ToApp, s.ToWallet, tenant.CompanyName, _ => gt, ct);
            return $"Göndərildi: app {app}, wallet {wallet}, hədiyyə: {giftText ?? "yox"}";
        }

        private async Task ProcessTenantBirthdaysAsync(TenantBirthdaySetting s, bool ignoreSendHour, CancellationToken ct)
        {
            var tenant = await LoadTenantAsync(s.TenantId, ct);
            if (tenant == null || !tenant.CanSend) return;
            var localNow = TenantClock.LocalNow(tenant.Tz);
            if (!ignoreSendHour && localNow.Hour < s.SendHourLocal) return;
            var today = localNow.Date;

            var keys = BirthdateKeys(today);
            var maxDob = today.AddYears(-BirthDateRules.MinPlausibleAge);
            var m1 = keys[0].Month; var d1 = keys[0].Day;
            var m2 = keys.Count > 1 ? keys[1].Month : -1; var d2 = keys.Count > 1 ? keys[1].Day : -1;

            var candidates = await _db.Customers.AsNoTracking()
                .Where(c => c.TenantId == s.TenantId && c.IsActive && c.DateOfBirth != null && c.DateOfBirth <= maxDob
                            && ((c.DateOfBirth.Value.Month == m1 && c.DateOfBirth.Value.Day == d1)
                                || (c.DateOfBirth.Value.Month == m2 && c.DateOfBirth.Value.Day == d2)))
                .Select(c => new { c.Id, c.applicationUserId, c.FirstName, c.DateOfBirth, c.RegDate })
                .ToListAsync(ct);
            candidates = candidates.Where(c => BirthDateRules.IsUsable(c.DateOfBirth, c.RegDate, today) && BirthDateRules.IsBirthday(c.DateOfBirth!.Value, today)).ToList();
            if (candidates.Count == 0) { await MarkRunAsync(s.Id, today, ct); return; }

            var ids = candidates.Select(c => c.Id).ToList();
            var done = await _db.BirthdayNotificationLogs.AsNoTracking()
                .Where(l => l.TenantId == s.TenantId && l.Year == today.Year && ids.Contains(l.CustomerId))
                .Select(l => l.CustomerId)
                .ToListAsync(ct);
            var todo = candidates.Where(c => !done.Contains(c.Id)).ToList();

            int recipients = 0, appTotal = 0, walletTotal = 0;
            foreach (var c in todo)
            {
                // 1) Idempotentlik: log sətri əvvəl (unikal index) — paralel run eyni müştərini iki dəfə götürə bilməz
                var log = new BirthdayNotificationLog
                {
                    TenantId = s.TenantId, CustomerId = c.Id, Year = today.Year, SentAtUtc = DateTime.UtcNow,
                    RegUserId = SystemUserId, RegDate = DateTime.UtcNow, IsActive = true
                };
                _db.BirthdayNotificationLogs.Add(log);
                try { await _db.SaveChangesAsync(ct); }
                catch (DbUpdateException) { _db.ChangeTracker.Clear(); continue; }

                // 2) Hədiyyə (istəyə bağlı)
                string? giftText = null;
                if (s.GiftEnabled && s.GiftValue > 0)
                {
                    try { giftText = await GrantBirthdayGiftAsync(s, c.Id, log, ct); }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Birthday gift failed. TenantId={TenantId} CustomerId={CustomerId}", s.TenantId, c.Id);
                        _db.ChangeTracker.Clear();
                    }
                }

                // 3) Personal bildiriş
                try
                {
                    var target = new Target(c.Id, c.applicationUserId, c.FirstName);
                    var gt = giftText;
                    var (app, wallet) = await DeliverAsync(new List<Target> { target }, s.Title, s.Body, s.ToApp, s.ToWallet, tenant.CompanyName, _ => gt, ct);
                    appTotal += app; walletTotal += wallet; recipients++;
                }
                catch (Exception ex) { _logger.LogWarning(ex, "Birthday notification failed. CustomerId={CustomerId}", c.Id); }
            }

            if (recipients > 0)
            {
                _db.TenantNotificationLogs.Add(new TenantNotificationLog
                {
                    TenantId = s.TenantId,
                    Scope = "birthday",
                    Title = Truncate(s.Title, 200),
                    Body = s.Body,
                    RecipientCount = recipients,
                    Source = TenantNotificationLog.SourceBirthday,
                    Channels = Channels(s.ToApp, s.ToWallet),
                    AudienceSummary = $"Ad günü ({today:dd.MM.yyyy})" + (s.GiftEnabled && s.GiftValue > 0 ? " · hədiyyə ilə" : ""),
                    AppSent = appTotal,
                    WalletSent = walletTotal,
                    RegUserId = SystemUserId,
                    RegDate = DateTime.UtcNow,
                    IsActive = true
                });
                await _db.SaveChangesAsync(ct);
                _logger.LogInformation("Birthday notifications sent. TenantId={TenantId} Recipients={Count}", s.TenantId, recipients);
            }
            await MarkRunAsync(s.Id, today, ct);
        }

        private static IReadOnlyList<(int Month, int Day)> BirthdateKeys(DateTime today) => BirthDateRules.BirthdayKeys(today);

        private async Task MarkRunAsync(int settingId, DateTime localToday, CancellationToken ct)
        {
            await _db.TenantBirthdaySettings.Where(x => x.Id == settingId)
                .ExecuteUpdateAsync(set => set.SetProperty(x => x.LastRunLocalDate, localToday), ct);
        }

        /// <summary>Müştərinin bu tenant-dakı aktiv kartına hədiyyə: xal kartı ₼ (müddətli ola bilər), möhür kartı möhür. Hədiyyə mətni qaytarır.</summary>
        private async Task<string?> GrantBirthdayGiftAsync(TenantBirthdaySetting s, int customerId, BirthdayNotificationLog log, CancellationToken ct)
        {
            var card = await _db.LoyaltyCards
                .Include(lc => lc.LoyaltyProgram).ThenInclude(p => p.CardTemplate)
                .Where(lc => lc.CustomerId == customerId && lc.TenantId == s.TenantId && lc.IsActive && lc.Status == CardStatus.Active)
                .OrderBy(lc => lc.Id)
                .FirstOrDefaultAsync(ct);
            if (card == null) return null;

            var template = card.LoyaltyProgram?.CardTemplate;
            var type = template?.EffectiveType ?? CardTemplateType.PointsBased;
            var isStamp = type == CardTemplateType.ProgressBased;
            var delta = isStamp ? Math.Floor(s.GiftValue) : s.GiftValue;
            if (delta <= 0) return null;

            BalanceMath.ApplyDelta(card, new CardTemplateSnapshot { TemplateType = type, TotalSteps = template?.TotalSteps ?? 10 }, delta);
            if (!isStamp) card.LifetimePoints += delta;
            card.EditDate = DateTime.UtcNow;

            var now = DateTime.UtcNow;
            _db.Transactions.Add(new Transaction
            {
                LoyaltyCardId = card.Id,
                Type = TransactionType.Bonus,
                Status = RedeemStatus.Applied,
                Points = delta,
                BalanceAfter = card.CurrentPoints,
                Description = BirthdayGiftDescription,
                OrderId = $"birthday:{card.Id}:{log.Year}",
                RegUserId = SystemUserId,
                RegDate = now,
                IsActive = true,
                LocalOccurredAt = DateTimeOffset.UtcNow,
                TimeZoneIdUsed = "UTC"
            });
            if (!isStamp && s.GiftExpiryDays.HasValue)
            {
                _db.BonusGrants.Add(new BonusGrant
                {
                    TenantId = s.TenantId, LoyaltyCardId = card.Id, Source = "birthday", Amount = delta,
                    GrantedAtUtc = now, ExpiresAtUtc = now.AddDays(s.GiftExpiryDays.Value), Status = BonusGrantStatus.Active,
                    RegUserId = SystemUserId, RegDate = now, IsActive = true
                });
            }
            log.GiftAmount = delta;
            log.LoyaltyCardId = card.Id;
            await _db.SaveChangesAsync(ct);

            // Wallet pass balansı (Apple + Google) — background; POS (iiko) balansı — xal kartı
            try { BackgroundJob.Enqueue<IWalletPushJob>(j => j.RunAsync(card.Id, card.LoyaltyProgramId, delta)); }
            catch (Exception ex) { _logger.LogWarning(ex, "Birthday gift wallet push enqueue failed. CardId={CardId}", card.Id); }
            if (!isStamp) await SyncPosSafeAsync(card.TenantId, card.CustomerId, card.CurrentPoints, ct);

            return NotificationTemplate.GiftText(delta, isStamp);
        }

        private async Task SyncPosSafeAsync(int tenantId, int customerId, decimal currentPoints, CancellationToken ct)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var sync = scope.ServiceProvider.GetRequiredService<IPosSyncService>();
                await sync.SyncBalanceToPosAsync(tenantId, customerId, currentPoints, ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Birthday gift POS sync failed. CustomerId={CustomerId}, TenantId={TenantId}", customerId, tenantId);
            }
        }
    }
}

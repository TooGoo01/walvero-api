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

        /// <summary>Alıcı; GiftText/DateText — {hədiyyə}/{tarix} (ad günü), CardId — hədiyyə yazılan kart (alıcı sətrinə).</summary>
        private sealed record Target(int Id, int? UserId, string FirstName, string? GiftText = null, string? DateText = null, int? CardId = null);
        private sealed record TenantInfo(int Id, string CompanyName, bool CanSend, int? Limit, TimeZoneInfo Tz);
        /// <summary>Real çatdırılma: Targets = kanalın hədəfi olan müştərilər, Sent = ≥1 kanal/cihaz uğurlu olan müştərilər.</summary>
        private sealed record DeliveryResult(int AppTargets, int AppSent, int WalletTargets, int WalletSent)
        {
            public static readonly DeliveryResult Empty = new(0, 0, 0, 0);
            public DeliveryResult Add(DeliveryResult o) => new(AppTargets + o.AppTargets, AppSent + o.AppSent, WalletTargets + o.WalletTargets, WalletSent + o.WalletSent);
        }

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
                .CountAsync(l => l.TenantId == tenantId && l.IsActive && l.RegDate >= start && l.RegDate < end
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
                .CountAsync(l => l.TenantId == tenantId && l.IsActive && l.RegDate >= start && l.RegDate < end && l.Source != TenantNotificationLog.SourceBirthday, ct);
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
            int logId = 0;
            try
            {
                var tenant = await LoadTenantAsync(row.TenantId, ct);
                if (tenant == null || !tenant.CanSend)
                    return await FailAsync(row, "Tenant aktiv deyil.", ct);

                var filter = AudienceFilterDto.Parse(row.AudienceJson);
                var today = TenantClock.LocalNow(tenant.Tz).Date;
                var targets = (await BuildAudienceQuery(row.TenantId, filter, row.BranchScopeId, today)
                    .Select(c => new { c.Id, c.applicationUserId, c.FirstName })
                    .ToListAsync(ct))
                    .Select(c => new Target(c.Id, c.applicationUserId, c.FirstName)).ToList();

                string? branchName = filter.BranchId.HasValue
                    ? await _db.Branches.AsNoTracking().Where(b => b.Id == filter.BranchId).Select(b => b.Name).FirstOrDefaultAsync(ct) : null;
                string? customerName = filter.CustomerId.HasValue
                    ? await _db.Customers.AsNoTracking().Where(c => c.Id == filter.CustomerId).Select(c => (c.FirstName + " " + c.LastName).Trim()).FirstOrDefaultAsync(ct) : null;

                // Log göndərişdən ƏVVƏL yazılır — alıcı sətirləri (TenantNotificationRecipients) FK ilə ona bağlanır.
                // Uğursuzluqda IsActive=false → kvota/tarixçəyə düşmür.
                var log = new TenantNotificationLog
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
                    RegUserId = row.RegUserId,
                    RegDate = DateTime.UtcNow,
                    IsActive = true
                };
                _db.TenantNotificationLogs.Add(log);
                await _db.SaveChangesAsync(ct);
                logId = log.Id;

                var d = await DeliverAsync(log.Id, row.TenantId, targets, row.Title, row.Body, row.ToApp, row.ToWallet, tenant.CompanyName, ct);

                log.AppTargets = d.AppTargets;
                log.AppSent = d.AppSent;
                log.WalletTargets = d.WalletTargets;
                log.WalletSent = d.WalletSent;
                row.Status = ScheduledNotificationStatus.Sent;
                row.SentAtUtc = DateTime.UtcNow;
                row.RecipientCount = targets.Count;
                row.EditDate = DateTime.UtcNow;
                await _db.SaveChangesAsync(ct);

                _logger.LogInformation("Tenant notification sent. Id={Id} TenantId={TenantId} Recipients={Count} App={App}/{AppTargets} Wallet={Wallet}/{WalletTargets} Immediate={Immediate}",
                    row.Id, row.TenantId, targets.Count, d.AppSent, d.AppTargets, d.WalletSent, d.WalletTargets, row.IsImmediate);
                return new Outcome(true, targets.Count, d.AppSent, d.WalletSent, null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Tenant notification failed. Id={Id} TenantId={TenantId}", row.Id, row.TenantId);
                if (logId > 0)
                {
                    try
                    {
                        _db.ChangeTracker.Clear();
                        await _db.TenantNotificationLogs.Where(l => l.Id == logId)
                            .ExecuteUpdateAsync(set => set.SetProperty(l => l.IsActive, false), ct);
                    }
                    catch (Exception ex2) { _logger.LogWarning(ex2, "Notification log deactivate failed. LogId={LogId}", logId); }
                }
                return await FailAsync(row, ex.Message, ct);
            }
        }

        private async Task<Outcome> FailAsync(ScheduledNotification row, string error, CancellationToken ct)
        {
            // Tracker vəziyyətindən asılı olmamaq üçün (uğursuz insert-dən sonra) birbaşa UPDATE
            _db.ChangeTracker.Clear();
            var now = DateTime.UtcNow;
            var err = Truncate(error, 500);
            await _db.ScheduledNotifications.Where(s => s.Id == row.Id)
                .ExecuteUpdateAsync(set => set
                    .SetProperty(s => s.Status, ScheduledNotificationStatus.Failed)
                    .SetProperty(s => s.Error, err)
                    .SetProperty(s => s.EditDate, now), ct);
            return new Outcome(false, 0, 0, 0, error);
        }

        private static bool IsUnfiltered(AudienceFilterDto f) =>
            !f.HasGenderFilter && !f.HasAgeFilter && !f.ActiveWithinDays.HasValue && !f.InactiveForDays.HasValue
            && !f.MinBalance.HasValue && !f.MaxBalance.HasValue && f.HasReadyGift != true && f.EffectiveChannel == "all";

        private static string Channels(bool app, bool wallet) => app && wallet ? "app,wallet" : app ? "app" : "wallet";
        private static string Truncate(string s, int max) => s.Length <= max ? s : s[..max];

        /// <summary>
        /// App (Notification sətri + FCM) və wallet (Apple/Google) çatdırılması; {ad}/{restoran}/{hədiyyə}/{tarix} personalizasiyası.
        /// Real nəticə: AppSent = ≥1 cihaza FCM qəbul edilən müştəri, WalletSent = ≥1 kanal uğurlu müştəri; Targets = kanal seçilibsə hədəf sayı.
        /// logId verilibsə hər alıcı üçün TenantNotificationRecipient sətri yazılır (geri dönüş attribution-u üçün).
        /// </summary>
        private async Task<DeliveryResult> DeliverAsync(
            int? logId, int tenantId, List<Target> targets, string title, string body, bool toApp, bool toWallet, string tenantName,
            CancellationToken ct)
        {
            if (targets.Count == 0) return DeliveryResult.Empty;
            var appOk = new HashSet<int>();
            var walletHasCard = new HashSet<int>();
            var walletOk = new HashSet<int>();
            int appTargetCount = 0;

            string T(Target t) => NotificationTemplate.Render(title, t.FirstName, tenantName, t.GiftText, t.DateText);
            string B(Target t) => NotificationTemplate.Render(body, t.FirstName, tenantName, t.GiftText, t.DateText);

            if (toApp)
            {
                var appTargets = targets.Where(t => t.UserId.HasValue).GroupBy(t => t.UserId!.Value).Select(g => g.First()).ToList();
                appTargetCount = appTargets.Count;
                if (appTargets.Count > 0)
                {
                    var now = DateTime.UtcNow;
                    var rows = appTargets.Select(t => new Notification
                    {
                        UserId = t.UserId!.Value,
                        Title = T(t),
                        Body = B(t),
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
                            if (await _push.SendToUserAsync(t.UserId!.Value, T(t), B(t)) > 0) appOk.Add(t.Id);
                        }
                        catch (Exception ex) { _logger.LogWarning(ex, "FCM push failed. UserId={UserId}", t.UserId); }
                    }
                }
            }

            if (toWallet)
            {
                foreach (var t in targets)
                {
                    try
                    {
                        var r = await _wallet.PushToCustomerAsync(t.Id, T(t), B(t), ct);
                        if (r.Cards > 0) walletHasCard.Add(t.Id);
                        if (r.Delivered > 0) walletOk.Add(t.Id);
                    }
                    catch (Exception ex) { _logger.LogWarning(ex, "Wallet message failed. CustomerId={CustomerId}", t.Id); }
                }
            }

            // Alıcı sətirləri — push döngülərindən SONRA (FCM servisi eyni DbContext-də stale token silərkən SaveChanges çağırır)
            if (logId.HasValue)
            {
                var now = DateTime.UtcNow;
                _db.TenantNotificationRecipients.AddRange(targets.DistinctBy(t => t.Id).Select(t => new TenantNotificationRecipient
                {
                    TenantNotificationLogId = logId.Value,
                    TenantId = tenantId,
                    CustomerId = t.Id,
                    LoyaltyCardId = t.CardId,
                    AppTarget = toApp && t.UserId.HasValue,
                    AppSent = appOk.Contains(t.Id),
                    WalletTarget = walletHasCard.Contains(t.Id),
                    WalletSent = walletOk.Contains(t.Id),
                    RegUserId = SystemUserId,
                    RegDate = now,
                    IsActive = true
                }));
                await _db.SaveChangesAsync(ct);
            }
            return new DeliveryResult(appTargetCount, appOk.Count, walletHasCard.Count, walletOk.Count);
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

        public async Task<ServiceResult> HistoryAsync(TenantNotificationContext ctx, int page, int pageSize, int windowDays = 7, CancellationToken ct = default)
        {
            var tenant = await LoadTenantAsync(ctx.TenantId, ct);
            if (tenant == null) return new ServiceResult(false, "Tenant tapılmadı.");
            page = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 5, 100);
            windowDays = Math.Clamp(windowDays, 1, 30);
            var q = _db.TenantNotificationLogs.AsNoTracking().Where(l => l.TenantId == ctx.TenantId && l.IsActive);
            if (ctx.BranchScopeId.HasValue) q = q.Where(l => l.BranchId == ctx.BranchScopeId);
            var total = await q.CountAsync(ct);
            var rows = await q.OrderByDescending(l => l.RegDate).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);

            // Geri dönüş: yalnız alıcı sətri olan (v3-dən sonrakı) göndərişlər üçün; köhnələr → hasAttribution=false
            var ids = rows.Select(l => l.Id).ToList();
            var withRecipients = (await _db.TenantNotificationRecipients.AsNoTracking()
                .Where(r => ids.Contains(r.TenantNotificationLogId))
                .Select(r => r.TenantNotificationLogId).Distinct().ToListAsync(ct)).ToHashSet();
            var attr = await NotificationAttribution.ByLogAsync(_db, ids, windowDays, ct);

            var items = rows.Select(l =>
            {
                var has = withRecipients.Contains(l.Id);
                attr.TryGetValue(l.Id, out var a);
                return new
                {
                    l.Id, l.Title, l.Body, l.Scope, l.Source, l.Channels, l.RecipientCount,
                    l.AppSent, l.WalletSent, l.AppTargets, l.WalletTargets, l.AudienceSummary,
                    sentAtLocal = Iso(TenantClock.ToLocal(l.RegDate, tenant.Tz)),
                    hasAttribution = has,
                    visits = has ? a.Visits : (int?)null,
                    visitRate = has && l.RecipientCount > 0 ? Math.Round(a.Visits * 100.0 / l.RecipientCount, 1) : (double?)null,
                    revenue = has ? a.Revenue : (decimal?)null,
                    windowDays
                };
            }).ToList();
            return new ServiceResult(true, new { items, total, page, pageSize, windowDays });
        }

        // =================== Ad günü ayarları ===================

        private const string DefaultBirthdayTitle = "Ad gününüz mübarək, {ad}!";
        private const string DefaultBirthdayBody = "{restoran} komandası sizi ad gününüz münasibətilə təbrik edir. Sizə {hədiyyə} hədiyyə etdik — növbəti ziyarətinizdə istifadə edin!";
        private const string DefaultBirthdayBodyNoGift = "{restoran} komandası sizi ad gününüz münasibətilə təbrik edir. Sizi gözləyirik!";
        // N gün əvvəl göndəriş üçün defaultlar ({tarix} = ad günü, dd.MM)
        private const string DefaultBirthdayTitleBefore = "{ad}, ad gününüz yaxınlaşır!";
        private const string DefaultBirthdayBodyBefore = "{tarix} tarixində ad gününüz var! {restoran} sizə indidən {hədiyyə} hədiyyə edir — gəlin, birlikdə qeyd edək!";
        private const string DefaultBirthdayBodyBeforeNoGift = "{tarix} tarixində ad gününüz var! {restoran} komandası sizi indidən təbrik edir — sizi gözləyirik!";
        public const int MaxDaysBefore = 14;

        private sealed record ProgramKinds(bool HasStamp, bool HasPoints, string FreeRewardLabel)
        {
            /// <summary>Seçilə bilən hədiyyə növləri (BirthdayGiftType int dəyərləri).</summary>
            public List<int> AllowedGiftTypes()
            {
                var list = new List<int>();
                if (HasPoints) list.Add((int)BirthdayGiftType.Points);
                if (HasStamp) { list.Add((int)BirthdayGiftType.Stamps); list.Add((int)BirthdayGiftType.FreeItem); }
                return list;
            }
            public bool Allows(BirthdayGiftType type) => type == BirthdayGiftType.Points ? HasPoints : HasStamp;
        }

        private async Task<ProgramKinds> ProgramKindsAsync(int tenantId, CancellationToken ct)
        {
            var templates = await _db.LoyaltyPrograms.AsNoTracking()
                .Where(p => p.TenantId == tenantId && p.CardTemplate != null)
                .OrderBy(p => p.Id)
                .Select(p => p.CardTemplate!)
                .ToListAsync(ct);
            var hasStamp = templates.Any(t => t.EffectiveType == CardTemplateType.ProgressBased);
            var hasPoints = templates.Any(t => t.EffectiveType != CardTemplateType.ProgressBased);
            var freeLabel = templates.Where(t => t.EffectiveType == CardTemplateType.ProgressBased)
                .Select(t => t.FreeRewardLabel).FirstOrDefault(l => !string.IsNullOrWhiteSpace(l))?.Trim() ?? "Pulsuz içki";
            return new ProgramKinds(hasStamp, hasPoints, freeLabel);
        }

        public async Task<ServiceResult> GetBirthdaySettingsAsync(int tenantId, CancellationToken ct = default)
        {
            var s = await _db.TenantBirthdaySettings.AsNoTracking().FirstOrDefaultAsync(x => x.TenantId == tenantId, ct);
            var kinds = await ProgramKindsAsync(tenantId, ct);
            var dto = s == null
                ? new BirthdaySettingsDto
                {
                    Enabled = false, SendHourLocal = 10, DaysBefore = 0, Title = DefaultBirthdayTitle, Body = DefaultBirthdayBodyNoGift, ToApp = true, ToWallet = true,
                    GiftType = kinds.HasPoints || !kinds.HasStamp ? BirthdayGiftType.Points : BirthdayGiftType.Stamps
                }
                : new BirthdaySettingsDto
                {
                    Enabled = s.Enabled, SendHourLocal = s.SendHourLocal, DaysBefore = s.DaysBefore, Title = s.Title, Body = s.Body, ToApp = s.ToApp, ToWallet = s.ToWallet,
                    GiftEnabled = s.GiftEnabled, GiftType = s.GiftType, GiftValue = s.GiftValue, GiftExpiryDays = s.GiftExpiryDays, GiftLabel = s.GiftLabel
                };
            return new ServiceResult(true, new
            {
                settings = dto,
                exists = s != null,
                hasStampProgram = kinds.HasStamp,
                hasPointsProgram = kinds.HasPoints,
                freeRewardLabel = kinds.FreeRewardLabel,
                allowedGiftTypes = kinds.AllowedGiftTypes(),
                maxDaysBefore = MaxDaysBefore,
                defaultTitle = DefaultBirthdayTitle,
                defaultTitleBefore = DefaultBirthdayTitleBefore,
                defaultBodyWithGift = DefaultBirthdayBody,
                defaultBodyNoGift = DefaultBirthdayBodyNoGift,
                defaultBodyBeforeWithGift = DefaultBirthdayBodyBefore,
                defaultBodyBeforeNoGift = DefaultBirthdayBodyBeforeNoGift,
                lastRunLocalDate = s?.LastRunLocalDate?.ToString("yyyy-MM-dd")
            });
        }

        public async Task<ServiceResult> SaveBirthdaySettingsAsync(TenantNotificationContext ctx, BirthdaySettingsDto dto, CancellationToken ct = default)
        {
            if (ctx.BranchScopeId.HasValue) return new ServiceResult(false, "Ad günü ayarlarını yalnız tenant admin dəyişə bilər.");
            if (dto.SendHourLocal is < 0 or > 23) return new ServiceResult(false, "Göndərmə saatı 0–23 olmalıdır.");
            if (dto.DaysBefore < 0 || dto.DaysBefore > MaxDaysBefore) return new ServiceResult(false, $"Ad günündən əvvəl 0–{MaxDaysBefore} gün ola bilər.");
            if (dto.Enabled && (string.IsNullOrWhiteSpace(dto.Title) || string.IsNullOrWhiteSpace(dto.Body)))
                return new ServiceResult(false, "Başlıq və mətn boş ola bilməz.");
            if (dto.Enabled && !dto.ToApp && !dto.ToWallet) return new ServiceResult(false, "Ən azı bir kanal seçilməlidir.");
            if ((dto.Title?.Length ?? 0) > 200 || (dto.Body?.Length ?? 0) > 1000) return new ServiceResult(false, "Başlıq ≤ 200, mətn ≤ 1000 simvol.");
            if (!Enum.IsDefined(typeof(BirthdayGiftType), dto.GiftType)) return new ServiceResult(false, "Hədiyyə növü düzgün deyil.");
            var giftLabel = string.IsNullOrWhiteSpace(dto.GiftLabel) ? null : dto.GiftLabel.Trim();
            if (giftLabel?.Length > 100) return new ServiceResult(false, "Hədiyyə mətni ≤ 100 simvol.");
            if (dto.GiftEnabled)
            {
                var kinds = await ProgramKindsAsync(ctx.TenantId, ct);
                if (!kinds.Allows(dto.GiftType)) return new ServiceResult(false, "Bu hədiyyə növü üçün uyğun kart proqramı yoxdur.");
                var isInt = dto.GiftValue == Math.Floor(dto.GiftValue);
                var valueErr = dto.GiftType switch
                {
                    BirthdayGiftType.Points => dto.GiftValue < 0.01m || dto.GiftValue > 1000 ? "Bonus 0,01–1000 ₼ olmalıdır." : null,
                    BirthdayGiftType.Stamps => dto.GiftValue < 1 || dto.GiftValue > 50 || !isInt ? "Möhür sayı 1–50 tam ədəd olmalıdır." : null,
                    _ => dto.GiftValue < 1 || dto.GiftValue > 5 || !isInt ? "Pulsuz məhsul sayı 1–5 tam ədəd olmalıdır." : null
                };
                if (valueErr != null) return new ServiceResult(false, valueErr);
            }
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
            s.DaysBefore = dto.DaysBefore;
            s.Title = (dto.Title ?? "").Trim();
            s.Body = (dto.Body ?? "").Trim();
            s.ToApp = dto.ToApp;
            s.ToWallet = dto.ToWallet;
            s.GiftEnabled = dto.GiftEnabled;
            s.GiftType = dto.GiftType;
            s.GiftValue = dto.GiftEnabled ? BirthdayGiftMath.Units(dto.GiftType, dto.GiftValue) : 0;
            s.GiftExpiryDays = dto.GiftEnabled && dto.GiftType == BirthdayGiftType.Points ? dto.GiftExpiryDays : null;
            s.GiftLabel = giftLabel;
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

        /// <summary>
        /// Ad günü kampaniyası nəticələri (son <paramref name="days"/> gün): göndəriş, hədiyyə (növ üzrə), uyğun kart tapılmayan,
        /// geri dönüş (göndərişdən sonrakı pəncərədə Earned/Redeemed tx-i olan), dövriyyə, müddətli bonus istifadəsi/itkisi (yalnız Points + müddət).
        /// </summary>
        public async Task<ServiceResult> BirthdayStatsAsync(int tenantId, int days = 365, int windowDays = 7, CancellationToken ct = default)
        {
            days = Math.Clamp(days, 7, 730);
            windowDays = Math.Clamp(windowDays, 1, 30);
            double window = windowDays;
            var since = DateTime.UtcNow.AddDays(-days);
            var logs = _db.BirthdayNotificationLogs.AsNoTracking().Where(l => l.TenantId == tenantId && l.SentAtUtc >= since);

            var sent = await logs.CountAsync(ct);
            var byType = await logs.Where(l => l.GiftAmount != null)
                .GroupBy(l => l.GiftType)
                .Select(g => new { giftType = g.Key, count = g.Count(), amount = g.Sum(l => l.GiftAmount!.Value) })
                .ToListAsync(ct);
            var noCard = await logs.CountAsync(l => l.GiftType != null && l.GiftAmount == null, ct);

            // Geri dönüş: müştərinin tenant-dakı hər hansı kartında pəncərə içində Applied Earned/Redeemed tx
            var visitedRows = await (
                from l in logs
                join c in _db.LoyaltyCards.AsNoTracking() on new { l.CustomerId, l.TenantId } equals new { c.CustomerId, c.TenantId }
                join t in _db.Transactions.AsNoTracking() on c.Id equals t.LoyaltyCardId
                where t.IsActive && t.Status == RedeemStatus.Applied
                      && (t.Type == TransactionType.Earned || t.Type == TransactionType.Redeemed)
                      && t.RegDate > l.SentAtUtc && t.RegDate <= l.SentAtUtc.AddDays(window)
                group t by l.Id into g
                select new { LogId = g.Key, Revenue = g.Sum(x => x.Type == TransactionType.Earned ? (x.Amount ?? 0m) : 0m) })
                .ToListAsync(ct);

            // Müddətli bonus (BonusGrant, yalnız Points + GiftExpiryDays): istifadə = Expired: Amount − ExpiredAmount; Active: grant-dan sonrakı Redeemed (≤ Amount)
            var grants = _db.BonusGrants.AsNoTracking()
                .Where(g => g.TenantId == tenantId && g.Source == "birthday" && g.IsActive && g.GrantedAtUtc >= since);
            var expired = await grants.Where(g => g.Status == BonusGrantStatus.Expired)
                .Select(g => new { g.Amount, Exp = g.ExpiredAmount ?? 0m }).ToListAsync(ct);
            var active = await grants.Where(g => g.Status == BonusGrantStatus.Active)
                .Select(g => new { g.LoyaltyCardId, g.Amount, g.GrantedAtUtc }).ToListAsync(ct);
            decimal activeUsed = 0;
            if (active.Count > 0)
            {
                var cardIds = active.Select(g => g.LoyaltyCardId).Distinct().ToList();
                var minGranted = active.Min(g => g.GrantedAtUtc);
                var spent = await _db.Transactions.AsNoTracking()
                    .Where(t => cardIds.Contains(t.LoyaltyCardId) && t.IsActive && t.Status == RedeemStatus.Applied
                                && t.Type == TransactionType.Redeemed && t.RegDate >= minGranted)
                    .Select(t => new { t.LoyaltyCardId, t.RegDate, t.Points })
                    .ToListAsync(ct);
                foreach (var g in active)
                {
                    var used = spent.Where(t => t.LoyaltyCardId == g.LoyaltyCardId && t.RegDate >= g.GrantedAtUtc).Sum(t => Math.Abs(t.Points));
                    activeUsed += Math.Min(used, g.Amount);
                }
            }

            var gifted = byType.Sum(x => x.count);
            var visited = visitedRows.Count;
            return new ServiceResult(true, new
            {
                days,
                windowDays,
                sent,
                gifted,
                noCard,
                byType,
                visited,
                visitRate = sent == 0 ? 0 : Math.Round(visited * 100.0 / sent, 1),
                revenue = visitedRows.Sum(x => x.Revenue),
                trackedBonus = expired.Sum(e => e.Amount) + active.Sum(g => g.Amount),
                giftUsed = expired.Sum(e => Math.Max(0, e.Amount - e.Exp)) + activeUsed,
                giftExpired = expired.Sum(e => e.Exp),
                giftActive = active.Count
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

        /// <summary>Ad günü namizədi: Birthday = real ad günü (bu il/gələn il), DaysUntil = bu gündən qalan gün (0 = bu gün).</summary>
        private sealed record BirthdayCandidate(int Id, int? UserId, string FirstName, DateTime Birthday, int DaysUntil);

        public async Task<string> RunBirthdayForCustomerAsync(int tenantId, int customerId, CancellationToken ct = default)
        {
            var s = await _db.TenantBirthdaySettings.AsNoTracking().FirstOrDefaultAsync(x => x.TenantId == tenantId, ct);
            if (s == null) return "Ad günü ayarı yoxdur — əvvəl saxlayın (söndürülü də olar).";
            var c = await _db.Customers.AsNoTracking()
                .Where(x => x.Id == customerId && x.TenantId == tenantId && x.IsActive)
                .Select(x => new { x.Id, x.applicationUserId, x.FirstName, x.DateOfBirth })
                .FirstOrDefaultAsync(ct);
            if (c == null) return "Müştəri tapılmadı.";
            var tenant = await LoadTenantAsync(tenantId, ct);
            if (tenant == null || !tenant.CanSend) return "Tenant aktiv deyil.";
            var today = TenantClock.LocalNow(tenant.Tz).Date;

            // Test: ad günü aralıqdadırsa real tarix, yoxsa "bu gün" kimi (Year = bu il — köhnə davranış)
            var bday = c.DateOfBirth.HasValue ? BirthDateRules.BirthdayWithin(c.DateOfBirth.Value, today, s.DaysBefore) ?? today : today;
            var cand = new BirthdayCandidate(c.Id, c.applicationUserId, c.FirstName, bday, (bday - today).Days);
            if (await _db.BirthdayNotificationLogs.AsNoTracking().AnyAsync(l => l.TenantId == tenantId && l.CustomerId == c.Id && l.Year == bday.Year, ct))
                return "Bu müştəri bu il artıq təbrik olunub (idempotentlik).";

            var summaryId = await CreateBirthdaySummaryLogAsync(s, today, 1, ct);
            var d = await SendBirthdayAsync(s, tenant, summaryId, cand, ct);
            await FinalizeBirthdaySummaryLogAsync(summaryId, d == null ? 0 : 1, d ?? DeliveryResult.Empty, ct);
            if (d == null) return "Bu müştəri bu il artıq təbrik olunub (idempotentlik).";
            return $"Göndərildi: app {d.AppSent}/{d.AppTargets}, wallet {d.WalletSent}/{d.WalletTargets}, ad günü {bday:dd.MM} ({cand.DaysUntil} gün)";
        }

        private async Task ProcessTenantBirthdaysAsync(TenantBirthdaySetting s, bool ignoreSendHour, CancellationToken ct)
        {
            var tenant = await LoadTenantAsync(s.TenantId, ct);
            if (tenant == null || !tenant.CanSend) return;
            var localNow = TenantClock.LocalNow(tenant.Tz);
            if (!ignoreSendHour && localNow.Hour < s.SendHourLocal) return;
            var today = localNow.Date;

            var todo = await FindBirthdayCandidatesAsync(s, today, ct);
            if (todo.Count == 0) { await MarkRunAsync(s.Id, today, ct); return; }

            // Xülasə log döngüdən ƏVVƏL — alıcı sətirləri ona bağlanır; sonda sayğaclar yenilənir (0 alıcı → silinir)
            var summaryId = await CreateBirthdaySummaryLogAsync(s, today, todo.Count, ct);
            var total = DeliveryResult.Empty;
            var recipients = 0;
            foreach (var c in todo)
            {
                try
                {
                    var d = await SendBirthdayAsync(s, tenant, summaryId, c, ct);
                    if (d == null) continue;
                    recipients++;
                    total = total.Add(d);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Birthday notification failed. TenantId={TenantId} CustomerId={CustomerId}", s.TenantId, c.Id);
                    _db.ChangeTracker.Clear();
                }
            }
            await FinalizeBirthdaySummaryLogAsync(summaryId, recipients, total, ct);
            if (recipients > 0)
                _logger.LogInformation("Birthday notifications sent. TenantId={TenantId} Recipients={Count} App={App}/{AppTargets} Wallet={Wallet}/{WalletTargets} DaysBefore={DaysBefore}",
                    s.TenantId, recipients, total.AppSent, total.AppTargets, total.WalletSent, total.WalletTargets, s.DaysBefore);
            await MarkRunAsync(s.Id, today, ct);
        }

        /// <summary>
        /// [bu gün, bu gün + DaysBefore] aralığında ad günü olan, hələ həmin ad günü (CustomerId, Year) üçün təbrik olunmamış müştərilər.
        /// Aralıq skanı: ayar 0→N dəyişəndə / job bir gün buraxanda heç kim atlanmır.
        /// </summary>
        private async Task<List<BirthdayCandidate>> FindBirthdayCandidatesAsync(TenantBirthdaySetting s, DateTime today, CancellationToken ct)
        {
            var keys = BirthDateRules.BirthdayKeysInRange(today, s.DaysBefore).ToList();
            var maxDob = today.AddYears(-BirthDateRules.MinPlausibleAge);
            var raw = await _db.Customers.AsNoTracking()
                .Where(c => c.TenantId == s.TenantId && c.IsActive && c.DateOfBirth != null && c.DateOfBirth <= maxDob
                            && keys.Contains(c.DateOfBirth.Value.Month * 100 + c.DateOfBirth.Value.Day))
                .Select(c => new { c.Id, c.applicationUserId, c.FirstName, c.DateOfBirth, c.RegDate })
                .ToListAsync(ct);

            var list = new List<BirthdayCandidate>();
            foreach (var c in raw)
            {
                if (!BirthDateRules.IsUsable(c.DateOfBirth, c.RegDate, today)) continue;
                var bday = BirthDateRules.BirthdayWithin(c.DateOfBirth!.Value, today, s.DaysBefore);
                if (bday == null) continue;
                list.Add(new BirthdayCandidate(c.Id, c.applicationUserId, c.FirstName, bday.Value, (bday.Value - today).Days));
            }
            if (list.Count == 0) return list;

            var ids = list.Select(c => c.Id).ToList();
            var years = list.Select(c => c.Birthday.Year).Distinct().ToList();
            var done = await _db.BirthdayNotificationLogs.AsNoTracking()
                .Where(l => l.TenantId == s.TenantId && years.Contains(l.Year) && ids.Contains(l.CustomerId))
                .Select(l => new { l.CustomerId, l.Year })
                .ToListAsync(ct);
            var doneSet = done.Select(d => (d.CustomerId, d.Year)).ToHashSet();
            return list.Where(c => !doneSet.Contains((c.Id, c.Birthday.Year))).OrderBy(c => c.DaysUntil).ThenBy(c => c.Id).ToList();
        }

        /// <summary>Bir müştəriyə ad günü axını: log sətri (idempotent) → hədiyyə → şəxsi bildiriş. null = bu ad günü üçün artıq göndərilib.</summary>
        private async Task<DeliveryResult?> SendBirthdayAsync(TenantBirthdaySetting s, TenantInfo tenant, int summaryLogId, BirthdayCandidate c, CancellationToken ct)
        {
            var now = DateTime.UtcNow;
            var giftOn = s.GiftEnabled && s.GiftValue > 0;

            // 1) Idempotentlik: log sətri əvvəl (unikal index) — paralel run eyni müştərini iki dəfə götürə bilməz
            var log = new BirthdayNotificationLog
            {
                TenantId = s.TenantId, CustomerId = c.Id, Year = c.Birthday.Year, SentAtUtc = now,
                GiftType = giftOn ? (int)s.GiftType : null,
                RegUserId = SystemUserId, RegDate = now, IsActive = true
            };
            _db.BirthdayNotificationLogs.Add(log);
            try { await _db.SaveChangesAsync(ct); }
            catch (DbUpdateException) { _db.ChangeTracker.Clear(); return null; }

            // 2) Hədiyyə (istəyə bağlı) — uyğun kart yoxdursa mətn null qalır
            string? giftText = null; int? cardId = null;
            if (giftOn)
            {
                try { (giftText, cardId) = await GrantBirthdayGiftAsync(s, c.Id, log, c.DaysUntil, ct); }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Birthday gift failed. TenantId={TenantId} CustomerId={CustomerId}", s.TenantId, c.Id);
                    _db.ChangeTracker.Clear();
                }
            }

            // 3) Şəxsi bildiriş — hədiyyə verilməyibsə {hədiyyə}-li mətn əvəzinə hədiyyəsiz default (müştəriyə boş vəd olmasın)
            var before = c.DaysUntil > 0;
            var title = giftOn && giftText == null && NotificationTemplate.HasGiftPlaceholder(s.Title)
                ? (before ? DefaultBirthdayTitleBefore : DefaultBirthdayTitle) : s.Title;
            var body = giftOn && giftText == null && NotificationTemplate.HasGiftPlaceholder(s.Body)
                ? (before ? DefaultBirthdayBodyBeforeNoGift : DefaultBirthdayBodyNoGift) : s.Body;
            var target = new Target(c.Id, c.UserId, c.FirstName, giftText, c.Birthday.ToString("dd.MM"), cardId);
            return await DeliverAsync(summaryLogId, s.TenantId, new List<Target> { target }, title, body, s.ToApp, s.ToWallet, tenant.CompanyName, ct);
        }

        private async Task<int> CreateBirthdaySummaryLogAsync(TenantBirthdaySetting s, DateTime today, int plannedCount, CancellationToken ct)
        {
            var giftOn = s.GiftEnabled && s.GiftValue > 0;
            var log = new TenantNotificationLog
            {
                TenantId = s.TenantId,
                Scope = "birthday",
                Title = Truncate(s.Title, 200),
                Body = s.Body,
                RecipientCount = plannedCount,
                Source = TenantNotificationLog.SourceBirthday,
                Channels = Channels(s.ToApp, s.ToWallet),
                AudienceSummary = $"Ad günü ({today:dd.MM.yyyy}" + (s.DaysBefore > 0 ? $", {s.DaysBefore} gün əvvəl" : "") + ")"
                                  + (giftOn ? " · hədiyyə ilə" : ""),
                RegUserId = SystemUserId,
                RegDate = DateTime.UtcNow,
                IsActive = true
            };
            _db.TenantNotificationLogs.Add(log);
            await _db.SaveChangesAsync(ct);
            _db.Entry(log).State = EntityState.Detached;
            return log.Id;
        }

        /// <summary>Sayğaclar birbaşa UPDATE ilə (döngüdə ChangeTracker.Clear ola bilər); 0 alıcı → sətir silinir.</summary>
        private async Task FinalizeBirthdaySummaryLogAsync(int logId, int recipients, DeliveryResult total, CancellationToken ct)
        {
            if (recipients == 0)
            {
                await _db.TenantNotificationLogs.Where(l => l.Id == logId).ExecuteDeleteAsync(ct);
                return;
            }
            await _db.TenantNotificationLogs.Where(l => l.Id == logId)
                .ExecuteUpdateAsync(set => set
                    .SetProperty(l => l.RecipientCount, recipients)
                    .SetProperty(l => l.AppTargets, total.AppTargets)
                    .SetProperty(l => l.AppSent, total.AppSent)
                    .SetProperty(l => l.WalletTargets, total.WalletTargets)
                    .SetProperty(l => l.WalletSent, total.WalletSent), ct);
        }

        private async Task MarkRunAsync(int settingId, DateTime localToday, CancellationToken ct)
        {
            await _db.TenantBirthdaySettings.Where(x => x.Id == settingId)
                .ExecuteUpdateAsync(set => set.SetProperty(x => x.LastRunLocalDate, localToday), ct);
        }

        /// <summary>
        /// Müştərinin ayardakı növə uyğun ilk aktiv kartına hədiyyə: Points → xal/tier kartına ₼ (müddət ad günündən), Stamps → möhür,
        /// FreeItem → CompletedCycles +N. (mətn, kartId) qaytarır; uyğun kart yoxdursa mətn null (bildiriş hədiyyəsiz gedir).
        /// </summary>
        private async Task<(string? Text, int? CardId)> GrantBirthdayGiftAsync(TenantBirthdaySetting s, int customerId, BirthdayNotificationLog log, int daysUntilBirthday, CancellationToken ct)
        {
            var cards = await _db.LoyaltyCards
                .Include(lc => lc.LoyaltyProgram).ThenInclude(p => p.CardTemplate)
                .Where(lc => lc.CustomerId == customerId && lc.TenantId == s.TenantId && lc.IsActive && lc.Status == CardStatus.Active)
                .OrderBy(lc => lc.Id)
                .ToListAsync(ct);
            static CardTemplateType TypeOf(LoyaltyCard lc) => lc.LoyaltyProgram?.CardTemplate?.EffectiveType ?? CardTemplateType.PointsBased;
            var card = cards.FirstOrDefault(lc => BirthdayGiftMath.IsCompatible(s.GiftType, TypeOf(lc)));
            if (card == null)
            {
                _logger.LogWarning("Birthday gift skipped: no compatible card. TenantId={TenantId} CustomerId={CustomerId} GiftType={GiftType} Cards={Cards}",
                    s.TenantId, customerId, s.GiftType, cards.Count);
                return (null, cards.FirstOrDefault()?.Id);
            }

            var template = card.LoyaltyProgram?.CardTemplate;
            var type = TypeOf(card);
            var isPoints = s.GiftType == BirthdayGiftType.Points;
            var totalSteps = template?.TotalSteps ?? 10;
            var delta = BirthdayGiftMath.Delta(s.GiftType, s.GiftValue, totalSteps);
            if (delta <= 0) return (null, card.Id);

            BalanceMath.ApplyDelta(card, new CardTemplateSnapshot { TemplateType = type, TotalSteps = totalSteps }, delta);
            if (isPoints) card.LifetimePoints += delta;
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
            if (isPoints && s.GiftExpiryDays.HasValue)
            {
                _db.BonusGrants.Add(new BonusGrant
                {
                    TenantId = s.TenantId, LoyaltyCardId = card.Id, Source = "birthday", Amount = delta,
                    GrantedAtUtc = now, ExpiresAtUtc = BirthdayGiftMath.ExpiryUtc(now, daysUntilBirthday, s.GiftExpiryDays.Value),
                    Status = BonusGrantStatus.Active,
                    RegUserId = SystemUserId, RegDate = now, IsActive = true
                });
            }
            var units = BirthdayGiftMath.Units(s.GiftType, s.GiftValue);
            log.GiftAmount = units;
            log.GiftType = (int)s.GiftType;
            log.LoyaltyCardId = card.Id;
            await _db.SaveChangesAsync(ct);

            // Wallet pass balansı (Apple + Google) — background; POS (iiko) balansı — yalnız xal
            try { BackgroundJob.Enqueue<IWalletPushJob>(j => j.RunAsync(card.Id, card.LoyaltyProgramId, delta)); }
            catch (Exception ex) { _logger.LogWarning(ex, "Birthday gift wallet push enqueue failed. CardId={CardId}", card.Id); }
            if (isPoints) await SyncPosSafeAsync(card.TenantId, card.CustomerId, card.CurrentPoints, ct);

            return (NotificationTemplate.GiftText(units, s.GiftType, template?.FreeRewardLabel, s.GiftLabel), card.Id);
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

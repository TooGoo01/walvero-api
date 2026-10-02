using Loyalty.Business.Helpers;
using Loyalty.Business.Services.Abstractions.Main;
using Loyalty.DataAccess.Context;
using Loyalty.DataAccess.Entities.Main;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Loyalty.WebAPI.Controllers
{
    // DİQQƏT: class-level authorize YALNIZ authenticated tələb edir (policy YOX).
    // Policy method-də verilir — çünki class+method authorize AND-lənir (stack), override YOX.
    // Əvvəl class-da SuperAdminOnly idi → tenant-admin endpoint-ləri (MyQuota/SendToMyTenant) 403 alırdı.
    [Route("api/[controller]/[action]")]
    [ApiController]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public class NotificationController : ControllerBase
    {
        private readonly INotificationService _notificationService;
        private readonly IWalletMessagingService _walletMessaging;
        private readonly LoyaltyWalletDbContext _db;
        private readonly ITenantProvider _tenantProvider;
        private readonly ITenantNotificationService _tenantNotifications;
        private readonly INotificationJobs _notificationJobs;

        public NotificationController(
            INotificationService notificationService,
            IWalletMessagingService walletMessaging,
            LoyaltyWalletDbContext db,
            ITenantProvider tenantProvider,
            ITenantNotificationService tenantNotifications,
            INotificationJobs notificationJobs)
        {
            _notificationService = notificationService;
            _walletMessaging = walletMessaging;
            _db = db;
            _tenantProvider = tenantProvider;
            _tenantNotifications = tenantNotifications;
            _notificationJobs = notificationJobs;
        }

        // Xüsusi userə göndər (tenantId lazım deyil)
        public sealed record SendToUserRequest(int UserId, string Title, string Body, bool ToApp = true, bool ToWallet = false);

        [HttpPost]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Policy = "SuperAdminOnly")]
        public async Task<IActionResult> SendToUser([FromBody] SendToUserRequest request, CancellationToken ct)
        {
            if (!request.ToApp && !request.ToWallet)
                return BadRequest(new { success = false, message = "At least one channel (App or Wallet) must be selected." });

            int appDelivered = 0;
            int walletDelivered = 0;

            if (request.ToApp)
            {
                var notification = new Notification
                {
                    UserId = request.UserId,
                    Title = request.Title,
                    Body = request.Body,
                    IsRead = false,
                    CreatedAt = DateTime.UtcNow,
                    RegUserId = 1,
                    RegDate = DateTime.UtcNow,
                    IsActive = true
                };
                await _db.Notifications.AddAsync(notification, ct);
                await _db.SaveChangesAsync(ct);

                await _notificationService.SendToUserAsync(request.UserId, request.Title, request.Body);
                appDelivered = 1;
            }

            if (request.ToWallet)
            {
                var walletResult = await _walletMessaging.SendToUserAsync(request.UserId, request.Title, request.Body, ct);
                walletDelivered = walletResult.Data is int n ? n : 0;
            }

            return Ok(new { success = true, message = "Notification göndərildi.", appDelivered, walletDelivered });
        }

        // Customer-ə göndər (customer-də ApplicationUser yoxdursa app-i silently skip et)
        public sealed record SendToCustomerRequest(int CustomerId, string Title, string Body, bool ToApp = true, bool ToWallet = false);

        [HttpPost]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Policy = "SuperAdminOnly")]
        public async Task<IActionResult> SendToCustomer([FromBody] SendToCustomerRequest request, CancellationToken ct)
        {
            if (!request.ToApp && !request.ToWallet)
                return BadRequest(new { success = false, message = "At least one channel (App or Wallet) must be selected." });

            var customer = await _db.Customers
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == request.CustomerId && c.IsActive, ct);

            if (customer == null)
                return NotFound(new { success = false, message = $"Customer {request.CustomerId} not found or inactive." });

            bool appDelivered = false;
            bool appSkipped = false;
            int walletDelivered = 0;

            if (request.ToApp)
            {
                if (customer.applicationUserId.HasValue)
                {
                    var appUserId = customer.applicationUserId.Value;
                    var notification = new Notification
                    {
                        UserId = appUserId,
                        Title = request.Title,
                        Body = request.Body,
                        IsRead = false,
                        CreatedAt = DateTime.UtcNow,
                        RegUserId = 1,
                        RegDate = DateTime.UtcNow,
                        IsActive = true
                    };
                    await _db.Notifications.AddAsync(notification, ct);
                    await _db.SaveChangesAsync(ct);

                    await _notificationService.SendToUserAsync(appUserId, request.Title, request.Body);
                    appDelivered = true;
                }
                else
                {
                    appSkipped = true;
                }
            }

            if (request.ToWallet)
            {
                var walletResult = await _walletMessaging.SendToCustomerAsync(request.CustomerId, request.Title, request.Body, ct);
                walletDelivered = walletResult.Data is int n ? n : 0;
            }

            return Ok(new
            {
                success = true,
                message = appSkipped
                    ? $"App skipped (no app account). Wallet: {walletDelivered} channel."
                    : $"App: {(appDelivered ? "sent" : "off")}, Wallet: {walletDelivered} channel.",
                appDelivered,
                appSkipped,
                walletDelivered
            });
        }

        // Bütün userlərə göndər (tenantId lazım deyil)
        public sealed record SendToAllRequest(string Title, string Body, bool ToApp = true, bool ToWallet = false);

        [HttpPost]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Policy = "SuperAdminOnly")]
        public async Task<IActionResult> SendToAll([FromBody] SendToAllRequest request, CancellationToken ct)
        {
            if (!request.ToApp && !request.ToWallet)
                return BadRequest(new { success = false, message = "At least one channel (App or Wallet) must be selected." });

            int sentCount = 0;
            int walletDelivered = 0;

            if (request.ToApp)
            {
                // DeviceToken.UserId nullable oldu — guest tokenləri (UserId=null)
                // Notification (in-app) üçün istifadə oluna bilməz, ona görə filter edirik.
                var userIds = await _db.DeviceTokens
                    .Where(dt => dt.UserId.HasValue)
                    .Select(dt => dt.UserId!.Value)
                    .Distinct()
                    .ToListAsync(ct);

                if (userIds.Count > 0)
                {
                    var notifications = userIds.Select(uid => new Notification
                    {
                        UserId = uid,
                        Title = request.Title,
                        Body = request.Body,
                        IsRead = false,
                        CreatedAt = DateTime.UtcNow,
                        RegUserId = 1,
                        RegDate = DateTime.UtcNow,
                        IsActive = true
                    }).ToList();

                    await _db.Notifications.AddRangeAsync(notifications, ct);
                    await _db.SaveChangesAsync(ct);

                    await _notificationService.SendToAllDevicesAsync(request.Title, request.Body);
                    sentCount = userIds.Count;
                }
            }

            if (request.ToWallet)
            {
                var walletResult = await _walletMessaging.SendToAllAsync(request.Title, request.Body, ct);
                walletDelivered = walletResult.Data is int n ? n : 0;
            }

            return Ok(new
            {
                success = true,
                message = $"App: {sentCount} user, Wallet: {walletDelivered} channel.",
                sentCount,
                walletDelivered
            });
        }

        // Xüsusi tenant-ın bütün userlərinə göndər
        public sealed record SendToTenantRequest(int TenantId, string Title, string Body, bool ToApp = true, bool ToWallet = false);

        [HttpPost]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Policy = "SuperAdminOnly")]
        public async Task<IActionResult> SendToTenant([FromBody] SendToTenantRequest request, CancellationToken ct)
        {
            if (!request.ToApp && !request.ToWallet)
                return BadRequest(new { success = false, message = "At least one channel (App or Wallet) must be selected." });

            int sentCount = 0;
            int walletDelivered = 0;

            if (request.ToApp)
            {
                // Tenant-ın bütün aktiv user-lərini tap
                var userIds = await _db.Customers
                    .Where(c => c.TenantId == request.TenantId && c.IsActive && c.applicationUserId != null)
                    .Select(c => c.applicationUserId!.Value)
                    .Distinct()
                    .ToListAsync(ct);

                if (userIds.Count > 0)
                {
                    var notifications = userIds.Select(uid => new Notification
                    {
                        UserId = uid,
                        Title = request.Title,
                        Body = request.Body,
                        IsRead = false,
                        CreatedAt = DateTime.UtcNow,
                        RegUserId = 1,
                        RegDate = DateTime.UtcNow,
                        IsActive = true
                    }).ToList();

                    await _db.Notifications.AddRangeAsync(notifications, ct);
                    await _db.SaveChangesAsync(ct);

                    await _notificationService.SendToTenantUsersAsync(request.TenantId, request.Title, request.Body);
                    sentCount = userIds.Count;
                }
            }

            if (request.ToWallet)
            {
                var walletResult = await _walletMessaging.SendToTenantAsync(request.TenantId, request.Title, request.Body, ct);
                walletDelivered = walletResult.Data is int n ? n : 0;
            }

            return Ok(new
            {
                success = true,
                message = $"App: {sentCount} user, Wallet: {walletDelivered} channel.",
                sentCount,
                walletDelivered
            });
        }

        // ─────────────── Tenant admin bölməsi (v2: filtr, planlı, ad günü, tarixçə) ───────────────

        /// <summary>
        /// Tenant: X-Tenant-Id header yalnız Super Admin üçün etibarlıdır (BranchController.ResolveTenantId nümunəsi) —
        /// tenant admin header-i dəyişib başqa tenant-a göndərə / onun kvotasını işlədə bilməsin.
        /// Branch Admin / Branch Moderator / Moderator yalnız öz filialına (branch_id claim) göndərir.
        /// </summary>
        private TenantNotificationContext? ResolveContext(out IActionResult? error)
        {
            error = null;
            var isSuper = User.IsInRole("Super Admin");
            int? tenantId = null;
            if (isSuper && Request.Headers.TryGetValue("X-Tenant-Id", out var hv) && int.TryParse(hv, out var hid))
                tenantId = hid;
            else
                tenantId = TokenTenantId() ?? (isSuper ? _tenantProvider.CurrentTenantId : null);
            if (!tenantId.HasValue)
            {
                error = BadRequest(new { success = false, message = "Tenant not found." });
                return null;
            }

            int? branchScope = null;
            var branchScoped = !isSuper && !User.IsInRole("Tenant Admin")
                               && (User.IsInRole("Branch Admin") || User.IsInRole("Branch Moderator") || User.IsInRole("Moderator"));
            if (branchScoped)
            {
                branchScope = int.TryParse(User.FindFirst("branch_id")?.Value, out var b) ? b : null;
                if (!branchScope.HasValue)
                {
                    error = StatusCode(403, new { success = false, message = "Filial təyin olunmayıb — bildiriş göndərmək mümkün deyil." });
                    return null;
                }
            }
            var userId = int.TryParse(_tenantProvider.CurrentUserId, out var uid) ? uid : 1;
            return new TenantNotificationContext(tenantId.Value, userId, branchScope);
        }

        /// <summary>Token-dəki tenant claim-i (header nəzərə alınmır).</summary>
        private int? TokenTenantId()
        {
            foreach (var name in new[] { "tenant_id", "TenantId" })
                if (int.TryParse(User.FindFirst(name)?.Value, out var v)) return v;
            return null;
        }

        private IActionResult FromResult(Loyalty.Business.Results.ServiceResult r)
            => r.Success
                ? Ok(new { success = true, message = r.Message, data = r.Data })
                : BadRequest(new { success = false, message = r.Message });

        /// <summary>Tenant admin: bu ayın bildiriş kvotası (limit null = limitsiz; ad günü sayılmır; planlı göndərişlər yer tutur).</summary>
        [HttpGet]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Policy = "TenantAdminOrModerator")]
        public async Task<IActionResult> MyQuota(CancellationToken ct)
        {
            var ctx = ResolveContext(out var err);
            if (ctx == null) return err!;
            var r = await _tenantNotifications.GetQuotaAsync(ctx.TenantId, ct);
            if (!r.Success) return BadRequest(new { success = false, message = r.Message });
            // Köhnə cavab forması saxlanılır: { success, limit, used, remaining }
            var q = (NotificationQuotaDto)r.Data!;
            return Ok(new { success = true, limit = q.Limit, used = q.Used, remaining = q.Remaining });
        }

        /// <summary>Filtrə uyğun müştəri sayı (canlı önizləmə).</summary>
        [HttpPost]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Policy = "TenantAdminOrModerator")]
        public async Task<IActionResult> PreviewAudience([FromBody] AudienceFilterDto filter, CancellationToken ct)
        {
            var ctx = ResolveContext(out var err);
            if (ctx == null) return err!;
            return FromResult(await _tenantNotifications.PreviewAudienceAsync(ctx, filter ?? new AudienceFilterDto(), ct));
        }

        /// <summary>
        /// Tenant admin öz müştərilərinə bildiriş: hamısı / filial / fərdi / filtrli (cins, yaş, aktivlik, balans, kanal);
        /// ScheduledAtLocal verilərsə gələcək tarixə planlaşdırılır. Aylıq kvota enforce olunur.
        /// </summary>
        [HttpPost]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Policy = "TenantAdminOrModerator")]
        public async Task<IActionResult> SendToMyTenant([FromBody] SendTenantNotificationRequest request, CancellationToken ct)
        {
            var ctx = ResolveContext(out var err);
            if (ctx == null) return err!;
            return FromResult(await _tenantNotifications.SendOrScheduleAsync(ctx, request, ct));
        }

        [HttpGet]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Policy = "TenantAdminOrModerator")]
        public async Task<IActionResult> MyScheduled(CancellationToken ct)
        {
            var ctx = ResolveContext(out var err);
            if (ctx == null) return err!;
            return FromResult(await _tenantNotifications.ListScheduledAsync(ctx, ct));
        }

        [HttpPut("{id:int}")]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Policy = "TenantAdminOrModerator")]
        public async Task<IActionResult> UpdateScheduled(int id, [FromBody] SendTenantNotificationRequest request, CancellationToken ct)
        {
            var ctx = ResolveContext(out var err);
            if (ctx == null) return err!;
            return FromResult(await _tenantNotifications.UpdateScheduledAsync(ctx, id, request, ct));
        }

        [HttpPost("{id:int}")]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Policy = "TenantAdminOrModerator")]
        public async Task<IActionResult> CancelScheduled(int id, CancellationToken ct)
        {
            var ctx = ResolveContext(out var err);
            if (ctx == null) return err!;
            return FromResult(await _tenantNotifications.CancelScheduledAsync(ctx, id, ct));
        }

        [HttpGet]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Policy = "TenantAdminOrModerator")]
        public async Task<IActionResult> MyHistory([FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
        {
            var ctx = ResolveContext(out var err);
            if (ctx == null) return err!;
            return FromResult(await _tenantNotifications.HistoryAsync(ctx, page, pageSize, ct));
        }

        [HttpGet]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Policy = "TenantAdminOrModerator")]
        public async Task<IActionResult> MyBirthdaySettings(CancellationToken ct)
        {
            var ctx = ResolveContext(out var err);
            if (ctx == null) return err!;
            return FromResult(await _tenantNotifications.GetBirthdaySettingsAsync(ctx.TenantId, ct));
        }

        [HttpPut]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Policy = "TenantAdminOrModerator")]
        public async Task<IActionResult> SaveMyBirthdaySettings([FromBody] BirthdaySettingsDto dto, CancellationToken ct)
        {
            var ctx = ResolveContext(out var err);
            if (ctx == null) return err!;
            return FromResult(await _tenantNotifications.SaveBirthdaySettingsAsync(ctx, dto, ct));
        }

        [HttpGet]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Policy = "TenantAdminOrModerator")]
        public async Task<IActionResult> UpcomingBirthdays([FromQuery] int days = 7, CancellationToken ct = default)
        {
            var ctx = ResolveContext(out var err);
            if (ctx == null) return err!;
            return FromResult(await _tenantNotifications.UpcomingBirthdaysAsync(ctx.TenantId, days, ct));
        }

        /// <summary>
        /// Super admin: job-ları YALNIZ bir tenant üçün indi işlət (lokal / real test). Planlı (vaxtı çatanlar) + ad günü
        /// (ignoreSendHour=true → göndərmə saatını gözləmir). tenantId məcburidir — bütün tenant-lar üçün işləmir.
        /// </summary>
        [HttpPost]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Policy = "SuperAdminOnly")]
        public async Task<IActionResult> RunJobsNow(
            [FromQuery] int tenantId, [FromQuery] bool birthdays = false, [FromQuery] bool scheduled = true,
            [FromQuery] int? birthdayCustomerId = null, [FromQuery] bool expireBonuses = false,
            [FromServices] IPointsExpiryService? pointsExpiry = null, CancellationToken ct = default)
        {
            if (tenantId <= 0) return BadRequest(new { success = false, message = "tenantId tələb olunur." });
            var log = new List<string>();
            if (scheduled) { await _notificationJobs.RunScheduledAsync(tenantId, ct); log.Add("planlı: işlədi"); }
            // birthdayCustomerId: yalnız o müştəri (test) — tenantın digər ad günü sahiblərinə toxunmur
            if (birthdayCustomerId.HasValue) log.Add("ad günü (tək müştəri): " + await _notificationJobs.RunBirthdayForCustomerAsync(tenantId, birthdayCustomerId.Value, ct));
            else if (birthdays) { await _notificationJobs.RunBirthdaysAsync(tenantId, true, ct); log.Add("ad günü: işlədi"); }
            if (expireBonuses && pointsExpiry != null) { await pointsExpiry.ExpireBonusGrantsAsync(ct); log.Add("bonus müddəti: yoxlandı"); }
            return Ok(new { success = true, message = string.Join(" · ", log) });
        }
    }
}

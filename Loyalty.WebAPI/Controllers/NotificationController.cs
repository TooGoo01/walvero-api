using Loyalty.Business.Services.Abstractions.Main;
using Loyalty.DataAccess.Context;
using Loyalty.DataAccess.Entities.Main;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Loyalty.WebAPI.Controllers
{
    [Route("api/[controller]/[action]")]
    [ApiController]
    [Authorize(
        AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme,
        Policy = "SuperAdminOnly")]
    public class NotificationController : ControllerBase
    {
        private readonly INotificationService _notificationService;
        private readonly IWalletMessagingService _walletMessaging;
        private readonly LoyaltyWalletDbContext _db;
        private readonly ITenantProvider _tenantProvider;

        public NotificationController(
            INotificationService notificationService,
            IWalletMessagingService walletMessaging,
            LoyaltyWalletDbContext db,
            ITenantProvider tenantProvider)
        {
            _notificationService = notificationService;
            _walletMessaging = walletMessaging;
            _db = db;
            _tenantProvider = tenantProvider;
        }

        // Xüsusi userə göndər (tenantId lazım deyil)
        public sealed record SendToUserRequest(int UserId, string Title, string Body, bool ToApp = true, bool ToWallet = false);

        [HttpPost]
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

        // ─────────────── Tenant admin bölməsi (kvota ilə) ───────────────

        /// <summary>Tenant admin: bu ayın bildiriş kvotası (limit null = limitsiz).</summary>
        [HttpGet]
        [Authorize(
            AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme,
            Policy = "TenantAdminOrModerator")]
        public async Task<IActionResult> MyQuota(CancellationToken ct)
        {
            var tenantId = _tenantProvider.CurrentTenantId;
            if (!tenantId.HasValue)
                return BadRequest(new { success = false, message = "Tenant not found." });

            var limit = await _db.Tenants.AsNoTracking()
                .Where(t => t.Id == tenantId.Value)
                .Select(t => t.MonthlyNotificationLimit)
                .FirstOrDefaultAsync(ct);

            var used = await CountUsedThisMonthAsync(tenantId.Value, ct);
            return Ok(new
            {
                success = true,
                limit,
                used,
                remaining = limit.HasValue ? Math.Max(0, limit.Value - used) : (int?)null
            });
        }

        public sealed record SendToMyTenantRequest(
            string Title, string Body,
            int? BranchId = null, int? CustomerId = null,
            bool ToApp = true, bool ToWallet = false);

        /// <summary>
        /// Tenant admin öz müştərilərinə bildiriş göndərir: hamısı / filial (o filialda
        /// əməliyyatı olanlar) / fərdi müştəri. Aylıq kvota (super admin təyin edir) enforce olunur.
        /// </summary>
        [HttpPost]
        [Authorize(
            AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme,
            Policy = "TenantAdminOrModerator")]
        public async Task<IActionResult> SendToMyTenant([FromBody] SendToMyTenantRequest request, CancellationToken ct)
        {
            if (!request.ToApp && !request.ToWallet)
                return BadRequest(new { success = false, message = "Ən azı bir kanal (App/Wallet) seçilməlidir." });
            if (string.IsNullOrWhiteSpace(request.Title) || string.IsNullOrWhiteSpace(request.Body))
                return BadRequest(new { success = false, message = "Başlıq və mətn boş ola bilməz." });

            var tenantId = _tenantProvider.CurrentTenantId;
            if (!tenantId.HasValue)
                return BadRequest(new { success = false, message = "Tenant not found." });

            // Kvota yoxlaması (limit null = limitsiz)
            var limit = await _db.Tenants.AsNoTracking()
                .Where(t => t.Id == tenantId.Value)
                .Select(t => t.MonthlyNotificationLimit)
                .FirstOrDefaultAsync(ct);
            var used = await CountUsedThisMonthAsync(tenantId.Value, ct);
            if (limit.HasValue && used >= limit.Value)
                return BadRequest(new { success = false, message = $"Aylıq bildiriş limitiniz bitib ({used}/{limit})." });

            // Hədəf müştərilər (yalnız ÖZ tenant-ı — scope enforce)
            List<Customer> targets;
            string scope;
            if (request.CustomerId.HasValue)
            {
                scope = "customer";
                var c = await _db.Customers.AsNoTracking()
                    .FirstOrDefaultAsync(x => x.Id == request.CustomerId.Value && x.TenantId == tenantId.Value && x.IsActive, ct);
                if (c == null)
                    return BadRequest(new { success = false, message = "Müştəri tapılmadı." });
                targets = new List<Customer> { c };
            }
            else if (request.BranchId.HasValue)
            {
                scope = "branch";
                // Filial tenant-a aiddirmi?
                var branchOk = await _db.Branches.AsNoTracking()
                    .AnyAsync(b => b.Id == request.BranchId.Value && b.TenantId == tenantId.Value, ct);
                if (!branchOk)
                    return BadRequest(new { success = false, message = "Filial tapılmadı." });

                // O filialda ən azı bir əməliyyatı olan müştərilər
                var customerIds = await _db.Transactions.AsNoTracking()
                    .Where(t => t.BranchId == request.BranchId.Value)
                    .Join(_db.LoyaltyCards, t => t.LoyaltyCardId, lc => lc.Id, (t, lc) => lc.CustomerId)
                    .Distinct()
                    .ToListAsync(ct);

                targets = await _db.Customers.AsNoTracking()
                    .Where(c => customerIds.Contains(c.Id) && c.TenantId == tenantId.Value && c.IsActive)
                    .ToListAsync(ct);
            }
            else
            {
                scope = "tenant";
                targets = await _db.Customers.AsNoTracking()
                    .Where(c => c.TenantId == tenantId.Value && c.IsActive)
                    .ToListAsync(ct);
            }

            int appDelivered = 0;
            int walletDelivered = 0;

            if (request.ToApp)
            {
                var userIds = targets
                    .Where(c => c.applicationUserId.HasValue)
                    .Select(c => c.applicationUserId!.Value)
                    .Distinct()
                    .ToList();

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

                    if (scope == "tenant")
                    {
                        await _notificationService.SendToTenantUsersAsync(tenantId.Value, request.Title, request.Body);
                    }
                    else
                    {
                        foreach (var uid in userIds)
                            await _notificationService.SendToUserAsync(uid, request.Title, request.Body);
                    }
                    appDelivered = userIds.Count;
                }
            }

            if (request.ToWallet)
            {
                if (scope == "tenant")
                {
                    var walletResult = await _walletMessaging.SendToTenantAsync(tenantId.Value, request.Title, request.Body, ct);
                    walletDelivered = walletResult.Data is int n ? n : 0;
                }
                else
                {
                    foreach (var c in targets)
                    {
                        var wr = await _walletMessaging.SendToCustomerAsync(c.Id, request.Title, request.Body, ct);
                        walletDelivered += wr.Data is int n ? n : 0;
                    }
                }
            }

            // Kvota logu — 1 göndəriş aksiyası = 1 sətir
            _db.TenantNotificationLogs.Add(new TenantNotificationLog
            {
                TenantId = tenantId.Value,
                Scope = scope,
                BranchId = request.BranchId,
                CustomerId = request.CustomerId,
                Title = request.Title,
                RecipientCount = targets.Count,
                RegUserId = int.TryParse(_tenantProvider.CurrentUserId, out var uid2) ? uid2 : 1,
                RegDate = DateTime.UtcNow,
                IsActive = true
            });
            await _db.SaveChangesAsync(ct);

            var newUsed = used + 1;
            return Ok(new
            {
                success = true,
                message = $"App: {appDelivered} user, Wallet: {walletDelivered} kanal. Kvota: {newUsed}{(limit.HasValue ? $"/{limit}" : "")}",
                appDelivered,
                walletDelivered,
                recipientCount = targets.Count,
                used = newUsed,
                limit
            });
        }

        /// <summary>Bu təqvim ayında istifadə olunan göndəriş sayı.</summary>
        private Task<int> CountUsedThisMonthAsync(int tenantId, CancellationToken ct)
        {
            var now = DateTime.UtcNow;
            var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
            return _db.TenantNotificationLogs.AsNoTracking()
                .CountAsync(l => l.TenantId == tenantId && l.RegDate >= monthStart, ct);
        }
    }
}

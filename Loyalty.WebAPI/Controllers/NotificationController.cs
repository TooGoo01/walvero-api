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

        public NotificationController(
            INotificationService notificationService,
            IWalletMessagingService walletMessaging,
            LoyaltyWalletDbContext db)
        {
            _notificationService = notificationService;
            _walletMessaging = walletMessaging;
            _db = db;
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
    }
}

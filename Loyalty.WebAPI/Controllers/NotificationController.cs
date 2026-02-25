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
        private readonly LoyaltyWalletDbContext _db;

        public NotificationController(
            INotificationService notificationService,
            LoyaltyWalletDbContext db)
        {
            _notificationService = notificationService;
            _db = db;
        }

        // Xüsusi userə göndər (tenantId lazım deyil)
        public sealed record SendToUserRequest(int UserId, string Title, string Body);

        [HttpPost]
        public async Task<IActionResult> SendToUser([FromBody] SendToUserRequest request, CancellationToken ct)
        {
            // In-app notification yarat
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

            // Push notification göndər
            await _notificationService.SendToUserAsync(request.UserId, request.Title, request.Body);

            return Ok(new { success = true, message = "Notification göndərildi." });
        }

        // Bütün userlərə göndər (tenantId lazım deyil)
        public sealed record SendToAllRequest(string Title, string Body);

        [HttpPost]
        public async Task<IActionResult> SendToAll([FromBody] SendToAllRequest request, CancellationToken ct)
        {
            // Bütün aktiv userləri tap
            var userIds = await _db.DeviceTokens
                .Select(dt => dt.UserId)
                .Distinct()
                .ToListAsync(ct);

            if (userIds.Count == 0)
                return Ok(new { success = true, message = "Aktiv user yoxdur.", sentCount = 0 });

            // Hər user üçün in-app notification yarat
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

            // Push notification göndər
            await _notificationService.SendToAllDevicesAsync(request.Title, request.Body);

            return Ok(new { success = true, message = $"{userIds.Count} userə notification göndərildi.", sentCount = userIds.Count });
        }

        // Xüsusi tenant-ın bütün userlərinə göndər
        public sealed record SendToTenantRequest(int TenantId, string Title, string Body);

        [HttpPost]
        public async Task<IActionResult> SendToTenant([FromBody] SendToTenantRequest request, CancellationToken ct)
        {
            // Tenant-ın bütün aktiv user-lərini tap
            var userIds = await _db.Customers
                .Where(c => c.TenantId == request.TenantId && c.IsActive && c.applicationUserId != null)
                .Select(c => c.applicationUserId!.Value)
                .Distinct()
                .ToListAsync(ct);

            if (userIds.Count == 0)
                return Ok(new { success = true, message = "Bu tenantda aktiv user yoxdur.", sentCount = 0 });

            // Hər user üçün in-app notification yarat
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

            // Push notification göndər
            await _notificationService.SendToTenantUsersAsync(request.TenantId, request.Title, request.Body);

            return Ok(new { success = true, message = $"{userIds.Count} userə notification göndərildi.", sentCount = userIds.Count });
        }
    }
}

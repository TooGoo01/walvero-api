using Google.Apis.Auth.OAuth2;
using Loyalty.Business.Services.Abstractions.Main;
using Loyalty.DataAccess.Context;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using static Org.BouncyCastle.Math.EC.ECCurve;

namespace Loyalty.Business.Services.Implementations.Main
{
    internal class FcmNotificationService : INotificationService
    {
        private readonly LoyaltyWalletDbContext _db;
        private readonly GoogleCredential _googleCredential;
        private readonly string _projectId;
        private readonly IConfiguration _config;
        public FcmNotificationService(IConfiguration config, LoyaltyWalletDbContext db)
        {
            _config = config;        // <-- bunu unutmusan
            _db = db;

            var fcmSection = _config.GetSection("Fcm");

            _projectId = fcmSection["ProjectId"];

            var serviceAccountJson = Environment.GetEnvironmentVariable("GOOGLE_SERVICE_ACCOUNT_KEY");
            if (!string.IsNullOrEmpty(serviceAccountJson))
            {
                _googleCredential = GoogleCredential
                    .FromJson(serviceAccountJson)
                    .CreateScoped("https://www.googleapis.com/auth/firebase.messaging");
            }
            else
            {
                var filePath = Path.Combine(
                    Directory.GetCurrentDirectory(),
                    fcmSection["ServiceAccountFile"]
                );
                _googleCredential = GoogleCredential
                    .FromFile(filePath)
                    .CreateScoped("https://www.googleapis.com/auth/firebase.messaging");
            }
        }

        private async Task<string> GetAccessTokenAsync()
        {
            return await _googleCredential.UnderlyingCredential.GetAccessTokenForRequestAsync();
        }

        private async Task<bool> SendMessageAsync(string deviceToken, object message)
        {
            var accessToken = await GetAccessTokenAsync();

            using var client = new HttpClient();
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", accessToken);

            var json = JsonConvert.SerializeObject(new { message });

            HttpResponseMessage response;

            try
            {
                response = await client.PostAsync(
                    $"https://fcm.googleapis.com/v1/projects/{_projectId}/messages:send",
                    new StringContent(json, Encoding.UTF8, "application/json")
                );
            }
            catch
            {
                Console.WriteLine($"❌ FCM NETWORK ERROR for token {deviceToken}");
                return false;
            }

            var responseBody = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                // UNREGISTERED / INVALID_ARGUMENT → stale token, delete from DB so it's not retried.
                var isStale = response.StatusCode == System.Net.HttpStatusCode.NotFound
                              || responseBody.Contains("UNREGISTERED", StringComparison.OrdinalIgnoreCase)
                              || responseBody.Contains("INVALID_ARGUMENT", StringComparison.OrdinalIgnoreCase);

                if (isStale)
                {
                    try
                    {
                        var stale = _db.DeviceTokens.Where(t => t.Token == deviceToken).ToList();
                        if (stale.Count > 0)
                        {
                            _db.DeviceTokens.RemoveRange(stale);
                            await _db.SaveChangesAsync();
                            Console.WriteLine($"🗑 Stale FCM token deleted: {deviceToken}");
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"⚠ Failed to delete stale token {deviceToken}: {ex.Message}");
                    }
                }
                else
                {
                    Console.WriteLine($@"
❌ FCM failed
Token: {deviceToken}
Status: {response.StatusCode}
Body: {responseBody}
");
                }
                return false;
            }

            Console.WriteLine($"✔ FCM sent: {responseBody}");
            return true;
        }


        public async Task<bool> SendToDeviceAsync(string deviceToken, string title, string body, object data = null)
        {
            return await SendMessageAsync(deviceToken, new
            {
                token = deviceToken,
                notification = new { title, body },
                data = data,
                android = new
    {
        priority = "HIGH",
        notification = new { title, body }
    },

    apns = new
    {
        headers = new Dictionary<string, string>
        {
            ["apns-push-type"] = "alert",
            ["apns-priority"]  = "10",
        },
        payload = new
        {
            aps = new Dictionary<string, object>
            {
                ["alert"] = new { title = title, body = body },
                ["sound"] = "default",
                ["content-available"] = 1
            }
        }
    }
            });
        }

        public async Task<int> SendToUserAsync(int userId, string title, string body, object data = null)
        {
            var tokens = _db.DeviceTokens
                .Where(x => x.UserId == userId)
                .Select(x => x.Token)
                .ToList();
            if (data == null)
            {
                data = new { info = "none" };
            }
            var ok = 0;
            foreach (var token in tokens)
                if (await SendToDeviceAsync(token, title, body, data)) ok++;
            return ok;
        }

        public async Task SendToAllDevicesAsync(string title, string body, object data = null)
        {
            var tokens = _db.DeviceTokens.Select(x => x.Token).ToList();
            if (data == null)
            {
                data = new { info = "none" };
            }
            foreach (var token in tokens)
                await SendToDeviceAsync(token, title, body, data);
        }

        public async Task SendToTenantUsersAsync(int tenantId, string title, string body, object data = null)
        {
            var userIds = _db.Customers
                .Where(c => c.TenantId == tenantId && c.IsActive && c.applicationUserId != null)
                .Select(c => c.applicationUserId.Value)
                .Distinct()
                .ToList();

            // DeviceToken.UserId nullable oldu — guest tokenləri (UserId=null) burada
            // istisna edirik, çünki bu metod konkret tenant-ın login olmuş customer-lərinə göndərir.
            var tokens = _db.DeviceTokens
                .Where(dt => dt.UserId.HasValue && userIds.Contains(dt.UserId.Value))
                .Select(dt => dt.Token)
                .ToList();

            if (data == null)
            {
                data = new { info = "none" };
            }
            foreach (var token in tokens)
                await SendToDeviceAsync(token, title, body, data);
        }

        public async Task SendVerificationCodeAsync(int userId, string code)
        {
            await SendToUserAsync(
                userId,
                "Verification Code",
                $"Your verification code is {code}",
                new { code }
            );
        }
    }
}

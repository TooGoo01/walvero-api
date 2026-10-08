using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Loyalty.Business.Services.Abstractions.Main
{
    public interface INotificationService
    {
        /// <summary>true = FCM qəbul etdi (2xx).</summary>
        Task<bool> SendToDeviceAsync(string deviceToken, string title, string body, object data = null);
        /// <summary>Uğurla qəbul edilən cihaz (token) sayı — 0 = heç bir cihaza çatmadı.</summary>
        Task<int> SendToUserAsync(int userId, string title, string body, object data = null);
        Task SendToAllDevicesAsync(string title, string body, object data = null);
        Task SendToTenantUsersAsync(int tenantId, string title, string body, object data = null);
        Task SendVerificationCodeAsync(int userId, string code);
    }
}

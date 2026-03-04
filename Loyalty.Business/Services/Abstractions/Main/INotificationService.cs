using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Loyalty.Business.Services.Abstractions.Main
{
    public interface INotificationService
    {
        Task SendToDeviceAsync(string deviceToken, string title, string body, object data = null);
        Task SendToUserAsync(int userId, string title, string body, object data = null);
        Task SendToAllDevicesAsync(string title, string body, object data = null);
        Task SendToTenantUsersAsync(int tenantId, string title, string body, object data = null);
        Task SendVerificationCodeAsync(int userId, string code);
    }
}

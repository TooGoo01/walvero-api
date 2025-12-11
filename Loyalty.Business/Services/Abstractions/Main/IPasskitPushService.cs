using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Loyalty.Business.Services.Abstractions.Main
{
    public interface IPasskitPushService
    {
        Task NotifyUpdatedAsync(int tenantId,string passTypeIdentifier, string serialNumber,string passName, CancellationToken ct = default);
        Task NotifyUpdatedMessageAsync(int tenantId,string passTypeIdentifier, string serialNumber,string header,string body, CancellationToken ct = default);
    }
}

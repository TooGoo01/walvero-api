using Loyalty.Business.Dtos.Post.Main;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Loyalty.Business.Services.Abstractions.Main
{
    public interface IDeviceTokenService
    {
        Task RegisterAsync(RegisterDeviceTokenRequest request);
        Task RemoveAsync(RemoveDeviceTokenRequest request);
    }
}

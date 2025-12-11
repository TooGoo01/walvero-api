using Loyalty.Business.Dtos.Post.Main;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Loyalty.Business.Services.Abstractions.Main
{
    public interface IWalletCredentialProvider
    {
        Task<AppleWalletRuntimeCreds> GetAppleAsync(int tenantId, CancellationToken ct = default);
        Task<GoogleWalletRuntimeConfig> GetGoogleAsync(int tenantId, CancellationToken ct = default);
    }
}

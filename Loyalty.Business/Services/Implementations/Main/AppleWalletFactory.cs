using Google.Apis.Http;
using Loyalty.Business.Dtos.Get.Main;
using Loyalty.Business.Dtos.Post.Main;
using Loyalty.Business.Services.Abstractions.Main;
using Loyalty.DataAccess.Entities.Main;
using Loyalty.DataAccess.Enums;
using Loyalty.DataAccess.Repositories.Abstractions.Main;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Loyalty.Business.Services.Implementations.Main
{
    public sealed class AppleWalletFactory : IAppleWalletFactory
    {
        private readonly IWalletCredentialProvider _provider;
        private readonly IServiceProvider _sp;
        public AppleWalletFactory(IWalletCredentialProvider provider, IServiceProvider sp)
        { _provider = provider; _sp = sp; }

        public async Task<IAppleWalletService> CreateAsync(int tenantId, CancellationToken ct = default)
        {
            var creds = await _provider.GetAppleAsync(tenantId, ct);
            return ActivatorUtilities.CreateInstance<AppleWalletService>(_sp, creds);
        }
    }
}

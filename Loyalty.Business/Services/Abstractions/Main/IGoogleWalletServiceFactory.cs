using Loyalty.Business.Dtos.Post.Main;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Loyalty.Business.Services.Abstractions.Main
{
    public interface IGoogleWalletServiceFactory
    {
        Task<IGoogleWalletService> CreateForTenantAsync(int tenantId, int loyaltyProgramId, CancellationToken ct = default);
    }
}

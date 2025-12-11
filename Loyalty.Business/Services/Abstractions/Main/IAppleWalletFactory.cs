using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Loyalty.Business.Services.Abstractions.Main
{
    public interface IAppleWalletFactory
    {
        Task<IAppleWalletService> CreateAsync(int tenantId, CancellationToken ct = default);
    }
}

using Loyalty.Business.Results;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Loyalty.Business.Services.Abstractions.Main
{
    public interface ILoyaltyCardService
    {
        Task<string> GenerateUniqueWalletCodeAsync(int tenantId, int programId, int userId, CancellationToken ct = default);
        Task<ServiceResult> GetByBarcodeValueAsync(int tenantId,string code);
        // (istəyə görə) kart yaratma helperi:
        // Task<LoyaltyCard> CreateCardAsync(int tenantId, int customerId, int programId, CancellationToken ct = default);
    }
}

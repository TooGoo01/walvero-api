using Loyalty.DataAccess.Entities.Main;
using Loyalty.DataAccess.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Loyalty.DataAccess.Repositories.Abstractions.Main
{
    public interface IWalletPassRepository : IGenericRepository<WalletPass>
    {
        Task<WalletPass?> GetBySerialNumberAsync(string serialNumber, CancellationToken cancellationToken = default);
        Task<WalletPass?> GetByCardAndPlatformAsync(int cardId, WalletPlatform platform, CancellationToken cancellationToken = default);
        Task<IEnumerable<WalletPass>> GetByCardIdAsync(int cardId, CancellationToken cancellationToken = default);
        Task<IEnumerable<WalletPass>> GetActivePasses(CancellationToken cancellationToken = default);
    }
}

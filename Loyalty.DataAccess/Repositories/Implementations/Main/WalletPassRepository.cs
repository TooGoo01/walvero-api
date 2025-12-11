using Loyalty.DataAccess.Context;
using Loyalty.DataAccess.Entities.Main;
using Loyalty.DataAccess.Enums;
using Loyalty.DataAccess.Repositories.Abstractions.Main;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Loyalty.DataAccess.Repositories.Implementations.Main
{
    public class WalletPassRepository : GenericRepository<WalletPass>, IWalletPassRepository
    {
        public WalletPassRepository(LoyaltyWalletDbContext LoyaltyDbContext) : base(LoyaltyDbContext)
        {
        }
        public async Task<WalletPass?> GetBySerialNumberAsync(string serialNumber, CancellationToken cancellationToken = default)
        {
            return await GetAsQueryable()
                .Include(wp => wp.LoyaltyCard)
                    .ThenInclude(lc => lc.Customer)
                .Include(wp => wp.LoyaltyCard)
                    .ThenInclude(lc => lc.LoyaltyProgram)
                        .ThenInclude(lp => lp.CardTemplate)
                .FirstOrDefaultAsync(wp => wp.SerialNumber == serialNumber, cancellationToken);
        }

        public async Task<WalletPass?> GetByCardAndPlatformAsync(int cardId, WalletPlatform platform, CancellationToken cancellationToken = default)
        {
            return await GetAsQueryable()
                .FirstOrDefaultAsync(wp => wp.LoyaltyCardId == cardId && wp.Platform == platform, cancellationToken);
        }

        public async Task<IEnumerable<WalletPass>> GetByCardIdAsync(int cardId, CancellationToken cancellationToken = default)
        {
            return await GetAsQueryable()
                .Where(wp => wp.LoyaltyCardId == cardId)
                .ToListAsync(cancellationToken);
        }

        public async Task<IEnumerable<WalletPass>> GetActivePasses(CancellationToken cancellationToken = default)
        {
            return await GetAsQueryable()
                .Where(wp => wp.Status == PassStatus.Active)
                .Include(wp => wp.LoyaltyCard)
                .ToListAsync(cancellationToken);
        }
    }
}

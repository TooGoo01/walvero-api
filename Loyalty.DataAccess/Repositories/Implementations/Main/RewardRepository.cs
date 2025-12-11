using Loyalty.DataAccess.Context;
using Loyalty.DataAccess.Entities.Main;
using Loyalty.DataAccess.Repositories.Abstractions.Main;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Loyalty.DataAccess.Repositories.Implementations.Main
{
    public class RewardRepository : GenericRepository<Reward>, IRewardRepository
    {
        public RewardRepository(LoyaltyWalletDbContext LoyaltyDbContext) : base(LoyaltyDbContext)
        {
        }
        public async Task<IEnumerable<Reward>> GetByProgramIdAsync(int programId, CancellationToken cancellationToken = default)
        {
            return await GetAsQueryable()
                .Where(r => r.LoyaltyProgramId == programId)
                .OrderBy(r => r.PointsCost)
                .ToListAsync(cancellationToken);
        }

        public async Task<IEnumerable<Reward>> GetActiveRewardsByProgramIdAsync(int programId, CancellationToken cancellationToken = default)
        {
            return await GetAsQueryable()
                .Where(r => r.LoyaltyProgramId == programId && r.IsActive)
                .OrderBy(r => r.PointsCost)
                .ToListAsync(cancellationToken);
        }

        public async Task<IEnumerable<Reward>> GetAvailableRewardsByPointsAsync(int programId, int points, CancellationToken cancellationToken = default)
        {
            return await GetAsQueryable()
                .Where(r => r.LoyaltyProgramId == programId
                    && r.IsActive
                    && r.PointsCost <= points
                    && (r.StockQuantity == null || r.StockQuantity > 0))
                .OrderBy(r => r.PointsCost)
                .ToListAsync(cancellationToken);
        }
    }
}

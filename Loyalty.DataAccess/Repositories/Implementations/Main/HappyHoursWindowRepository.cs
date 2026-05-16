using Loyalty.DataAccess.Context;
using Loyalty.DataAccess.Entities.Main;
using Loyalty.DataAccess.Repositories.Abstractions.Main;
using Microsoft.EntityFrameworkCore;

namespace Loyalty.DataAccess.Repositories.Implementations.Main
{
    public class HappyHoursWindowRepository : GenericRepository<HappyHoursWindow>, IHappyHoursWindowRepository
    {
        public HappyHoursWindowRepository(LoyaltyWalletDbContext loyaltyDbContext) : base(loyaltyDbContext)
        {
        }

        public Task<List<HappyHoursWindow>> GetByProgramAsync(int loyaltyProgramId, CancellationToken ct = default)
            => GetAsQueryable()
                .AsNoTracking()
                .Where(w => w.LoyaltyProgramId == loyaltyProgramId)
                .OrderBy(w => w.SpecificDate)
                .ThenBy(w => w.DayOfWeek)
                .ThenBy(w => w.StartTime)
                .ToListAsync(ct);

        public async Task DeleteByProgramAsync(int loyaltyProgramId, CancellationToken ct = default)
        {
            var existing = await GetAsQueryable()
                .Where(w => w.LoyaltyProgramId == loyaltyProgramId)
                .ToListAsync(ct);

            if (existing.Count > 0)
                _LoyaltyDbContext.Set<HappyHoursWindow>().RemoveRange(existing);
        }
    }
}

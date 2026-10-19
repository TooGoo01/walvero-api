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
    public class LoyaltyProgramRepository : GenericRepository<LoyaltyProgram>, ILoyaltyProgramRepository
    {
        public LoyaltyProgramRepository(LoyaltyWalletDbContext LoyaltyDbContext) : base(LoyaltyDbContext)
        {
        }

        public async Task<List<LoyaltyProgram>> GetActive(CancellationToken ct = default)
        {
            return await GetAsQueryable()
                .AsNoTracking()
                .Include(x => x.CardTemplate)
                .Where(x => x.IsActive == true)
                .ToListAsync(ct);
        }

        public Task<List<LoyaltyProgram>> GetActiveByTenantAsync(int tenantId, ProgramAudience? audience, CancellationToken ct = default)
        {
            var q = GetAsQueryable()
                .AsNoTracking()
                .Include(x => x.CardTemplate)
                .Where(x => x.TenantId == tenantId && x.IsActive);
            if (audience.HasValue) q = q.Where(x => x.Audience == audience.Value);
            return q.OrderBy(x => x.Id).ToListAsync(ct);
        }

        public Task<LoyaltyProgram?> GetAmbassadorProgramAsync(int tenantId, CancellationToken ct = default)
            => GetAsQueryable()
                .AsNoTracking()
                .Include(x => x.CardTemplate)
                .Where(x => x.TenantId == tenantId && x.IsActive && x.Audience == ProgramAudience.Ambassador)
                .OrderBy(x => x.Id)
                .FirstOrDefaultAsync(ct);

        public Task<LoyaltyProgram?> GetByCodeAsync(int tenantId, string code, CancellationToken ct = default)
        => GetAsQueryable().AsNoTracking().FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Code == code, ct);

        public async Task<LoyaltyProgram?> GetByIdNameAsync(int programId, CancellationToken ct = default)
        {
            // CardTemplate.Icons (1:N) → AsSplitQuery olmadan Cartesian product.
            return await GetAsQueryable()
                .AsNoTracking()
                .Include(x => x.CardTemplate)
                    .ThenInclude(t => t.Icons)
                .Include(x => x.HappyHoursWindows)
                .AsSplitQuery()
                .FirstOrDefaultAsync(x => x.Id == programId, ct);
        }

        public async Task<LoyaltyProgram?> GetByIdTrackedAsync(int programId, CancellationToken ct = default)
        {
          return await GetAsQueryable().FirstOrDefaultAsync(x => x.Id == programId, ct);
        }
        
        public Task<List<LoyaltyProgram>> ListAsync(int tenantId, bool onlyActive = true, CancellationToken ct = default)
        {
            var q = GetAsQueryable().AsNoTracking().Where(x => x.TenantId == tenantId);
            if (onlyActive) q = q.Where(x => x.IsActive);
            return q.OrderBy(x => x.ProgramName).ToListAsync(ct);
        }
        public async Task<List<LoyaltyProgram>> GetByIdsAsync(IEnumerable<int> programIds, CancellationToken ct = default)
        {
            var ids = programIds?.Distinct().ToList() ?? new List<int>();
            if (!ids.Any())
                return new List<LoyaltyProgram>();

            return await GetAsQueryable()
                .AsNoTracking()
                .Where(p => ids.Contains(p.Id))
                .Include(p => p.CardTemplate) // dizayn üçün lazım olacaqsa
                .ToListAsync(ct);
        }
    }
}

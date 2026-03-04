using Loyalty.DataAccess.Context;
using Loyalty.DataAccess.Entities.Main;
using Loyalty.DataAccess.Repositories.Abstractions.Main;
using Microsoft.EntityFrameworkCore;

namespace Loyalty.DataAccess.Repositories.Implementations.Main
{
    public class IntegrationTypeRepository : GenericRepository<IntegrationType>, IIntegrationTypeRepository
    {
        public IntegrationTypeRepository(LoyaltyWalletDbContext context) : base(context)
        {
        }

        public Task<IntegrationType?> GetBySourceAsync(string source, CancellationToken ct = default)
        {
            return GetAsQueryable()
                .FirstOrDefaultAsync(it => it.Source == source && it.IsActive, ct);
        }

        public async Task<IReadOnlyList<IntegrationType>> GetAllActiveAsync(CancellationToken ct = default)
        {
            return await GetAsQueryable()
                .Where(it => it.IsActive)
                .OrderBy(it => it.Name)
                .ToListAsync(ct);
        }
    }
}

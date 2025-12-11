using Loyalty.DataAccess.Context;
using Loyalty.DataAccess.Entities.Main;
using Loyalty.DataAccess.Enums;
using Loyalty.DataAccess.Repositories.Abstractions.Main;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Loyalty.DataAccess.Repositories.Implementations.Main
{
    public class CardTemplateRepository : GenericRepository<CardTemplate>, ICardTemplateRepository
    {
        public CardTemplateRepository(LoyaltyWalletDbContext LoyaltyDbContext) : base(LoyaltyDbContext)
        {
        }

        public async Task<CardTemplate> GetById(int id)
        {
            return await GetAsQueryable().Include(x=>x.Icons)
                .FirstOrDefaultAsync(ct => ct.Id==id);
        }

        public async Task<CardTemplate?> GetByLoyaltyProgramIdAsync(int programId, CancellationToken cancellationToken = default)
        {
            return await GetAsQueryable()
                .FirstOrDefaultAsync(ct => ct.LoyaltyProgramId == programId, cancellationToken);
        }

        public async Task<IEnumerable<CardTemplate>> GetByTemplateTypeAsync(CardTemplateType type, CancellationToken cancellationToken = default)
        {
            return await GetAsQueryable()
                .Where(ct => ct.TemplateType == type)
                .ToListAsync(cancellationToken);
        }
    }
}

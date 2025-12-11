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
    public class PartnerRepository : GenericRepository<Partner>, IPartnerRepository
    {
        public PartnerRepository(LoyaltyWalletDbContext MaxOilDbContext) : base(MaxOilDbContext)
        {
        }

        public Task<IEnumerable<Partner>> GetActive()
        {
            throw new NotImplementedException();
        }

        public async Task<IEnumerable<Partner>> GetAll()
        {
            return await GetAsQueryable()
              .ToListAsync();
        }

        public Task<IEnumerable<Partner>> GetAllAbouts()
        {
            throw new NotImplementedException();
        }

        public Task<Partner> GetById(int id)
        {
            throw new NotImplementedException();
        }

        public Task<Partner> GetLast()
        {
            throw new NotImplementedException();
        }
    }
}

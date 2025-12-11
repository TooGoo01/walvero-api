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
    public class ServiceRepository : GenericRepository<Service>, IServiceRepository
    {
        public ServiceRepository(LoyaltyWalletDbContext MaxOilDbContext) : base(MaxOilDbContext)
        {
        }

        public Task<IEnumerable<Service>> GetActive()
        {
            throw new NotImplementedException();
        }

        public async Task<IEnumerable<Service>> GetAll()
        {
            return await GetAsQueryable()
              .ToListAsync();
        }

        public Task<IEnumerable<Service>> GetAllAbouts()
        {
            throw new NotImplementedException();
        }

        public async Task<Service> GetById(int id)
        {
            return await GetAsQueryable().FirstOrDefaultAsync(x => x.Id == id);
        }

        public Task<Service> GetLast()
        {
            throw new NotImplementedException();
        }
    }
}

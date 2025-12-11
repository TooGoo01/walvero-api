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
    public class ProductRepository : GenericRepository<Product>, IProductRepository
    {
        public ProductRepository(LoyaltyWalletDbContext MaxOilDbContext) : base(MaxOilDbContext)
        {
        }

        public Task<IEnumerable<Product>> GetActive()
        {
            throw new NotImplementedException();
        }

        public async Task<IEnumerable<Product>> GetAll()
        {
            return await GetAsQueryable()
             .ToListAsync();
        }

        public Task<IEnumerable<Product>> GetAllAbouts()
        {
            throw new NotImplementedException();
        }

        public Task<Product> GetById(int id)
        {
            throw new NotImplementedException();
        }

        public Task<Product> GetLast()
        {
            throw new NotImplementedException();
        }
    }
}

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
    public class CategoryRepository : GenericRepository<Category>, ICategoryRepository
    {
        public CategoryRepository(LoyaltyWalletDbContext MaxOilDbContext) : base(MaxOilDbContext)
        {
        }

        public async Task<IEnumerable<Category>> GetActiveCategories()
        {
            return await GetAsQueryable().Where(x=>x.IsActive)
             .ToListAsync();
        }

        public async Task<IEnumerable<Category>> GetActiveCategoriesIdName()
        {
            return await GetAsQueryable().Where(x => x.IsActive)
            .ToListAsync();
        }

        public Task<Category> GetActiveCategory(int categoryId)
        {
            throw new NotImplementedException();
        }

        public async Task<IEnumerable<Category>> GetAllCategories()
        {
            return await GetAsQueryable()
              .ToListAsync();
        }

        public Task<Category> GetCategory(int categoryId)
        {
            throw new NotImplementedException();
        }

        public Task<IEnumerable<Category>> GetDeactiveCategories()
        {
            throw new NotImplementedException();
        }

        public Task<IEnumerable<Category>> GetDeActiveCategories()
        {
            throw new NotImplementedException();
        }

        public Task<Category> GetDeactiveCategory(int categoryId)
        {
            throw new NotImplementedException();
        }
    }
}

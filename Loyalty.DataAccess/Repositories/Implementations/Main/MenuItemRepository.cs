using Loyalty.DataAccess.Context;
using Loyalty.DataAccess.Entities.Main;
using Loyalty.DataAccess.Repositories.Abstractions.Main;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Loyalty.DataAccess.Repositories.Implementations.Main
{
    public class MenuItemRepository : GenericRepository<MenuItem>, IMenuItemRepository
    {
        public MenuItemRepository(LoyaltyWalletDbContext loyaltyDbContext) : base(loyaltyDbContext)
        {
        }

        public async Task<MenuItem?> GetByIdWithDetailsAsync(int id)
        {
            return await GetAsQueryable()
                .Include(mi => mi.MenuCategory)
                .Include(mi => mi.MenuItemImages)
                .Include(mi => mi.Variants)
                .FirstOrDefaultAsync(mi => mi.Id == id);
        }

        public async Task<IEnumerable<MenuItem>> GetByCategoryIdAsync(int categoryId)
        {
            return await GetAsQueryable()
                .Include(mi => mi.MenuItemImages)
                .Include(mi => mi.Variants)
                .Where(mi => mi.MenuCategoryId == categoryId && mi.IsActive)
                .ToListAsync();
        }

        public async Task<IEnumerable<MenuItem>> GetAllWithDetailsAsync()
        {
            return await GetAsQueryable()
                .Include(mi => mi.MenuCategory)
                .Include(mi => mi.MenuItemImages)
                .Include(mi => mi.Variants)
                .Where(mi => mi.IsActive)
                .ToListAsync();
        }

        public async Task<IEnumerable<MenuItem>> GetActiveAsync()
        {
            return await GetAsQueryable()
                .Where(mi => mi.IsActive)
                .ToListAsync();
        }
    }
}

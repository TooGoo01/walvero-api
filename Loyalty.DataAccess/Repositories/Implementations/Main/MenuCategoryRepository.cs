using Loyalty.DataAccess.Context;
using Loyalty.DataAccess.Entities.Main;
using Loyalty.DataAccess.Repositories.Abstractions.Main;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Loyalty.DataAccess.Repositories.Implementations.Main
{
    public class MenuCategoryRepository : GenericRepository<MenuCategory>, IMenuCategoryRepository
    {
        public MenuCategoryRepository(LoyaltyWalletDbContext loyaltyDbContext) : base(loyaltyDbContext)
        {
        }

        public async Task<MenuCategory?> GetByIdWithItemsAsync(int id)
        {
            return await GetAsQueryable()
                .Include(mc => mc.MenuItems)
                    .ThenInclude(mi => mi.MenuItemImages)
                .FirstOrDefaultAsync(mc => mc.Id == id);
        }

        public async Task<IEnumerable<MenuCategory>> GetAllWithItemsAsync()
        {
            return await GetAsQueryable()
                .Include(mc => mc.MenuItems)
                    .ThenInclude(mi => mi.MenuItemImages)
                .Where(mc => mc.IsActive)
                .ToListAsync();
        }

        public async Task<IEnumerable<MenuCategory>> GetActiveAsync()
        {
            return await GetAsQueryable()
                .Where(mc => mc.IsActive)
                .ToListAsync();
        }
    }
}

using Loyalty.DataAccess.Context;
using Loyalty.DataAccess.Entities.Main;
using Loyalty.DataAccess.Repositories.Abstractions.Main;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Loyalty.DataAccess.Repositories.Implementations.Main
{
    public class MenuItemImageRepository : GenericRepository<MenuItemImage>, IMenuItemImageRepository
    {
        public MenuItemImageRepository(LoyaltyWalletDbContext loyaltyDbContext) : base(loyaltyDbContext)
        {
        }

        public async Task<IEnumerable<MenuItemImage>> GetByMenuItemIdAsync(int menuItemId)
        {
            return await GetAsQueryable()
                .Where(mii => mii.MenuItemId == menuItemId && mii.IsActive)
                .ToListAsync();
        }

        public async Task<MenuItemImage?> GetByIdAsync(int id)
        {
            return await GetAsQueryable()
                .Include(mii => mii.MenuItem)
                .FirstOrDefaultAsync(mii => mii.Id == id);
        }
    }
}

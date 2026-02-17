using Loyalty.DataAccess.Entities.Main;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Loyalty.DataAccess.Repositories.Abstractions.Main
{
    public interface IMenuItemImageRepository : IGenericRepository<MenuItemImage>
    {
        Task<IEnumerable<MenuItemImage>> GetByMenuItemIdAsync(int menuItemId);
        Task<MenuItemImage?> GetByIdAsync(int id);
    }
}

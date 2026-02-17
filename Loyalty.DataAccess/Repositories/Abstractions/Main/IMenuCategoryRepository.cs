using Loyalty.DataAccess.Entities.Main;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Loyalty.DataAccess.Repositories.Abstractions.Main
{
    public interface IMenuCategoryRepository : IGenericRepository<MenuCategory>
    {
        Task<MenuCategory?> GetByIdWithItemsAsync(int id);
        Task<IEnumerable<MenuCategory>> GetAllWithItemsAsync();
        Task<IEnumerable<MenuCategory>> GetActiveAsync();
    }
}

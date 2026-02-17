using Loyalty.DataAccess.Entities.Main;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Loyalty.DataAccess.Repositories.Abstractions.Main
{
    public interface IMenuItemRepository : IGenericRepository<MenuItem>
    {
        Task<MenuItem?> GetByIdWithDetailsAsync(int id);
        Task<IEnumerable<MenuItem>> GetByCategoryIdAsync(int categoryId);
        Task<IEnumerable<MenuItem>> GetAllWithDetailsAsync();
        Task<IEnumerable<MenuItem>> GetActiveAsync();
    }
}

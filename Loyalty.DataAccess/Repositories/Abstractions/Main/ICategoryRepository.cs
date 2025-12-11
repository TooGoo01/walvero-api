

using Loyalty.DataAccess.Entities.Main;

namespace Loyalty.DataAccess.Repositories.Abstractions.Main
{
    public interface ICategoryRepository : IGenericRepository<Category>
    {
        Task<IEnumerable<Category>> GetActiveCategories();
        Task<IEnumerable<Category>> GetDeactiveCategories();
        Task<IEnumerable<Category>> GetActiveCategoriesIdName();
        Task<IEnumerable<Category>> GetAllCategories();
        Task<Category> GetCategory(int categoryId);
        Task<Category> GetActiveCategory(int categoryId);
        Task<Category> GetDeactiveCategory(int categoryId);

        Task<IEnumerable<Category>> GetDeActiveCategories();
    }
}

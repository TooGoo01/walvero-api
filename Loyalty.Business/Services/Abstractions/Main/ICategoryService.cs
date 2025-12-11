using Loyalty.Business.Dtos.Post.Main;
using Loyalty.Business.Results;
namespace Loyalty.Business.Services.Abstractions.Main
{
    public interface ICategoryService
    {
        Task<ServiceResult> AddCategory(AddCategoryDto category);
        Task<ServiceResult> GetCategories();
        Task<ServiceResult> GetActiveCategories();
        Task<ServiceResult> GetDeactiveCategories();
        Task<ServiceResult> GetActiveCategoriesIdName();
        Task<ServiceResult> GetCategory(int id);
        Task<ServiceResult> UpdateCategory(AddCategoryDto categoryDto, int id);
        Task<ServiceResult> DeleteCategory(int id);
        Task<ServiceResult> DeactiveCategory(int id);
        Task<ServiceResult> ActiveCategory(int id);
    }
}

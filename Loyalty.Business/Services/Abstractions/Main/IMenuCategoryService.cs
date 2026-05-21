using Loyalty.Business.Dtos.Get.Main;
using Loyalty.Business.Dtos.Post.Main;
using Loyalty.Business.Results;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Loyalty.Business.Services.Abstractions.Main
{
    public interface IMenuCategoryService
    {
        Task<ServiceResult> Add(AddMenuCategoryDto dto);
        Task<ServiceResult> Update(UpdateMenuCategoryDto dto);
        Task<ServiceResult> Delete(int id);
        Task<ServiceResult> GetById(int id);
        Task<ServiceResult> GetByIdWithItems(int id);
        Task<ServiceResult> GetAll();
        Task<ServiceResult> GetAllWithItems();
        Task<ServiceResult> GetActive();
        Task<ServiceResult> GetByTenantAndBranchAsync(int tenantId, int? branchId, bool branchScoped);
    }
}

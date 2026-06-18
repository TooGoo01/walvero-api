using Loyalty.Business.Dtos.Get.Main;
using Loyalty.Business.Dtos.Post.Main;
using Loyalty.Business.Results;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Loyalty.Business.Services.Abstractions.Main
{
    public interface IMenuItemService
    {
        Task<ServiceResult> Add(AddMenuItemDto dto);
        Task<ServiceResult> Update(UpdateMenuItemDto dto);
        Task<ServiceResult> Delete(int id);
        Task<ServiceResult> GetById(int id);
        Task<ServiceResult> GetByIdWithDetails(int id);
        Task<ServiceResult> GetByCategoryId(int categoryId);
        Task<ServiceResult> GetAll();
        Task<ServiceResult> GetAllWithDetails();
        Task<ServiceResult> GetActive();
        Task<ServiceResult> GetByTenantAndBranchAsync(int tenantId, int? branchId, bool branchScoped, int? categoryId);
        Task<ServiceResult> ReorderAsync(ReorderMenuItemDto dto);
    }
}

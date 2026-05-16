using Loyalty.Business.Dtos.Post.Main;
using Loyalty.Business.Results;

namespace Loyalty.Business.Services.Abstractions.Main
{
    public interface ITenantLinkService
    {
        Task<ServiceResult> Add(AddTenantLinkDto dto);
        Task<ServiceResult> Update(UpdateTenantLinkDto dto);
        Task<ServiceResult> Delete(int id);
        Task<ServiceResult> GetById(int id);
        Task<ServiceResult> GetByTenantId(int tenantId);
        Task<ServiceResult> Reorder(ReorderTenantLinksDto dto);
    }
}

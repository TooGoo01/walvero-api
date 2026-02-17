using Loyalty.Business.Dtos.Get.Main;
using Loyalty.Business.Dtos.Post.Main;
using Loyalty.Business.Results;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Loyalty.Business.Services.Abstractions.Main
{
    public interface IRestorantWifiInformationService
    {
        Task<ServiceResult> Add(AddRestorantWifiInformationDto dto);
        Task<ServiceResult> Update(UpdateRestorantWifiInformationDto dto);
        Task<ServiceResult> Delete(int id);
        Task<ServiceResult> GetById(int id);
        Task<ServiceResult> GetByIdWithTenant(int id);
        Task<ServiceResult> GetByTenantId(int tenantId);
        Task<ServiceResult> GetAll();
        Task<ServiceResult> GetAllWithTenant();
    }
}

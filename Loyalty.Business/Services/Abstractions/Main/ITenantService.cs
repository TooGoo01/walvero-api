using Loyalty.Business.Dtos.Get.Main;
using Loyalty.Business.Dtos.Post.Main;
using Loyalty.Business.Results;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Loyalty.Business.Services.Abstractions.Main
{
    public interface ITenantService
    {
        Task<ServiceResult> Add(AddTenantDto Dto);
        Task<ServiceResult> Get(int id);
        Task<ServiceResult> GetById(int id);
        Task<ServiceResult> GetAll();
        Task<ServiceResult> GetActive();
        Task<ServiceResult> GetActiveUser(int currentPage, int pageSize, TenantSearchDto model, int? currentUserId = null);
        Task<ServiceResult> GetLast();
        Task<ServiceResult> Update(UpdateTenantDto dto);
        Task<ServiceResult> Delete(int id);
        Task<ServiceResult> HasAnyRoleAsync(int userId,int tenantId,string[] roles);
        Task<IReadOnlyList<HomeTenantDto>> GetHomeTenantsAsync(int? countryId = null, int? cityId = null, CancellationToken ct = default);
    }
}

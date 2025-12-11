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
    public interface ITenantBrandingService
    {
        Task<ServiceResult> Add(AddTenantBrandingDto Dto);
        Task<ServiceResult> AddOrUpdateAsync(AddTenantBrandingDto dto, CancellationToken ct = default);
        Task<ServiceResult> Get(int id);
        Task<ServiceResult> GetByTenantId(int id);
        Task<ServiceResult> GetAll();
        Task<ServiceResult> GetLast();
        Task<ServiceResult> Update(AddTenantBrandingDto dto, int id);
        Task<ServiceResult> Delete(int id);
    }
}

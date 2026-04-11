using Loyalty.Business.Dtos.Get.Main;
using Loyalty.Business.Dtos.Post.Main;
using Loyalty.Business.Results;

namespace Loyalty.Business.Services.Abstractions.Main
{
    public interface ISalesRepVisitService
    {
        Task<ServiceResult> AddAsync(AddSalesRepVisitDto dto, CancellationToken ct = default);
        Task<ServiceResult> UpdateAsync(int id, UpdateSalesRepVisitDto dto, CancellationToken ct = default);
        Task<ServiceResult> DeleteAsync(int id, CancellationToken ct = default);
        Task<ServiceResult> GetByIdAsync(int id, CancellationToken ct = default);
        Task<ServiceResult> ListAsync(SalesRepVisitFilterDto filter, CancellationToken ct = default);
    }
}

using Loyalty.Business.Dtos.Get.Main;
using Loyalty.Business.Results;

namespace Loyalty.Business.Services.Abstractions.Main
{
    public interface IStatisticsService
    {
        Task<ServiceResult<DashboardStatisticsDto>> GetDashboardAsync(
            int tenantId,
            DateOnly startDate,
            DateOnly endDate,
            int? programId = null);

        /// <summary>Tenant-ın aktiv kartlı unikal müştəri sayı (tarix aralığından asılı deyil).</summary>
        Task<int> GetUniqueCustomerCountAsync(int tenantId, CancellationToken ct = default);
    }
}

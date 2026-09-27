using Loyalty.Business.Dtos.Get.Main;
using Loyalty.Business.Dtos.Post.Main;
using Loyalty.Business.Results;

namespace Loyalty.Business.Services.Abstractions.Main
{
    /// <summary>
    /// Admin üçün müştərinin iiko vəziyyəti: hər venue/org-da tapılırmı, wallet balansı vs bizim kart,
    /// və iki istiqamətli balans düzəlişi. Müştəri iiko-da yoxdursa yaratmır — yalnız göstərir.
    /// </summary>
    public interface IPosCustomerIikoService
    {
        Task<ServiceResult<PosCustomerIikoStatusDto>> GetStatusAsync(int tenantId, int customerId, int? cardId, CancellationToken ct = default);
        Task<ServiceResult<PosCustomerIikoStatusDto>> PushBalanceAsync(int tenantId, int customerId, IikoPushBalanceDto dto, CancellationToken ct = default);
        Task<ServiceResult<PosCustomerIikoStatusDto>> PullBalanceAsync(int tenantId, int customerId, IikoPullBalanceDto dto, CancellationToken ct = default);
    }
}

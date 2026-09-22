using Loyalty.Business.Dtos.Post.Main;
using Loyalty.Business.Results;

namespace Loyalty.Business.Services.Abstractions.Main
{
    /// <summary>
    /// Ambassador (promoter) müştəriləri: tenant admin yaradır, xal verir.
    /// Xal həm Walvero kartına, həm də POS inteqrasiyası varsa iiko wallet-inə forward olunur.
    /// </summary>
    public interface IAmbassadorService
    {
        Task<ServiceResult> CreateAmbassadorAsync(int tenantId, string userId, CreateAmbassadorDto dto, CancellationToken ct = default);
        Task<ServiceResult> AddPointsAsync(int tenantId, CreditPointsDto dto, CancellationToken ct = default);
    }
}

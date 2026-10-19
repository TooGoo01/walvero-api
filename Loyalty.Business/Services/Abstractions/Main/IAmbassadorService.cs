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
        /// <summary>Mövcud müştərini ambassador et (+ opsional ilkin balans). Proqramı auto-resolve edir.</summary>
        Task<ServiceResult> PromoteExistingAsync(int tenantId, string userId, PromoteAmbassadorDto dto, CancellationToken ct = default);
        /// <summary>Ambassadorluqdan çıxar: ambassador kartı bağlanır, adi kart (balansı ilə) geri açılır.</summary>
        Task<ServiceResult> DemoteAsync(int tenantId, string userId, DemoteAmbassadorDto dto, CancellationToken ct = default);
        Task<ServiceResult> AddPointsAsync(int tenantId, CreditPointsDto dto, CancellationToken ct = default);
        /// <summary>Kart balansının son istifadə tarixini dəyiş/sil. Keçmiş tarix → dərhal sıfırlama + iiko sync.</summary>
        Task<ServiceResult> SetPointsExpiryAsync(int tenantId, SetPointsExpiryDto dto, CancellationToken ct = default);
    }
}

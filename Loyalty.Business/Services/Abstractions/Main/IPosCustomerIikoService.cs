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
        /// <param name="lookupCardTrack">Əlavə iiko kart-track axtarışı (məs. köhnə sistemin fiziki kartı) — bizim barkod/walveroId-dən sonra, telefondan əvvəl.</param>
        Task<ServiceResult<PosCustomerIikoStatusDto>> GetStatusAsync(int tenantId, int customerId, int? cardId, string? lookupCardTrack = null, CancellationToken ct = default);
        Task<ServiceResult<PosCustomerIikoStatusDto>> PushBalanceAsync(int tenantId, int customerId, IikoPushBalanceDto dto, CancellationToken ct = default);
        Task<ServiceResult<PosCustomerIikoStatusDto>> PullBalanceAsync(int tenantId, int customerId, IikoPullBalanceDto dto, CancellationToken ct = default);
        /// <summary>Tapılan (mapping-siz) iiko qonağına bağla + köhnə balansını Walvero-ya əlavə et (X+Y) + iiko-nu bərabərləşdir.</summary>
        Task<ServiceResult<PosCustomerIikoStatusDto>> MergeByPhoneAsync(int tenantId, int customerId, IikoMergeByPhoneDto dto, CancellationToken ct = default);

        /// <summary>Toplu önizləmə (oxuma): inteqrasiyaya bağlanmamış, telefonu olan aktiv müştərilər kassada telefonla axtarılır.</summary>
        Task<ServiceResult<IikoPhoneMatchPreviewDto>> PreviewPhoneMatchAsync(int tenantId, int integrationId, int skip, int take, CancellationToken ct = default);

        /// <summary>Hangfire: bağlanmamış müştəriləri sync et (telefonla tap → bağla → köhnə balans əlavə → bərabərləşdir; tapılmayan yaradılır).</summary>
        Task<int> RunPhoneMatchBulkAsync(int tenantId, int integrationId, CancellationToken ct = default);

        /// <summary>
        /// iiko → Walvero toplu: proqramın aktiv kartları seçilmiş kassa cüzdanının balansına bərabərləşdirilir (iiko həqiqətdir).
        /// Möhür kartında dövrlər balansdan yenidən qurulur. Apply=false — önbaxış.
        /// </summary>
        Task<ServiceResult<IikoProgramPullResultDto>> PullProgramBalancesAsync(int tenantId, IikoProgramPullDto dto, CancellationToken ct = default);
    }
}

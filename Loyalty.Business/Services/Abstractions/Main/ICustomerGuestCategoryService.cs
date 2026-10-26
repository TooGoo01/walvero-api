using Loyalty.Business.Dtos.Post.Main;
using Loyalty.Business.Results;

namespace Loyalty.Business.Services.Abstractions.Main
{
    /// <summary>Kassa endirim səviyyəsi — müştərini iiko endirim kampaniyasının qonaq kateqoriyasına bağlayır.</summary>
    public interface ICustomerGuestCategoryService
    {
        /// <summary>
        /// Walvero-da saxla + iiko-da köhnə kateqoriyanı sil / yenisini yaz. Uğurda Data = PosCategorySyncResult,
        /// Message = admin üçün nəticə ("Kassaya yazıldı" / "kassada müştəri tapılmadı" …).
        /// </summary>
        Task<ServiceResult> SetAsync(int tenantId, SetCustomerPosCategoryDto dto, CancellationToken ct = default);
    }
}

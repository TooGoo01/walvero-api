using Loyalty.Business.Dtos.Main;
using Loyalty.Business.Results;

namespace Loyalty.Business.Services.Abstractions.Main
{
    public interface IReferencePlaceService
    {
        Task<ServiceResult> GetAllAsync(CancellationToken ct = default);
        Task<ServiceResult> AddAsync(CreateAiReferencePlaceDto dto, CancellationToken ct = default);
        Task<ServiceResult> UpdateAsync(UpdateAiReferencePlaceDto dto, CancellationToken ct = default);
        Task<ServiceResult> DeleteAsync(int id, CancellationToken ct = default);

        /// <summary>Chatbot üçün: aktiv reference məkanlarda keyword/ərazi axtarışı.</summary>
        /// <summary>Açar söz skoru; lat/lng + radiusKm verilibsə yalnız radius daxilindəki (koordinatlı) məkanlar, məsafə ilə.</summary>
        Task<List<GetAiReferencePlaceDto>> SearchAsync(string[] keywords, string? area, int take = 8,
            double? lat = null, double? lng = null, double? radiusKm = null, CancellationToken ct = default);

        /// <summary>Chatbot üçün: aktiv reference məkanı ada görə tap (normalize-contains, ən spesifik).</summary>
        Task<GetAiReferencePlaceDto?> FindByNameAsync(string name, CancellationToken ct = default);
    }
}

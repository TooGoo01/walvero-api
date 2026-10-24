using Loyalty.Business.Dtos.Main;
using Loyalty.Business.Results;

namespace Loyalty.Business.Services.Abstractions.Main
{
    /// <summary>Link seçimi səhifələri: super admin CRUD + public oxu (slug) + anonim klik.</summary>
    public interface ILinkPageService
    {
        Task<ServiceResult<List<GetLinkPageDto>>> ListAsync(CancellationToken ct = default);
        Task<ServiceResult<GetLinkPageDto>> GetAsync(int id, CancellationToken ct = default);
        Task<ServiceResult<GetLinkPageDto>> CreateAsync(CreateLinkPageDto dto, CancellationToken ct = default);
        Task<ServiceResult<GetLinkPageDto>> UpdateAsync(UpdateLinkPageDto dto, CancellationToken ct = default);
        Task<ServiceResult<bool>> DeleteAsync(int id, CancellationToken ct = default);
        Task<ServiceResult<LinkPageStatsDto>> GetStatsAsync(int id, DateTime? from, DateTime? to, CancellationToken ct = default);

        /// <summary>Public: dərc olunmuş səhifə (60 san yaddaş keşi); yalnız aktiv elementlər, klik sayı yoxdur.</summary>
        Task<ServiceResult<GetLinkPageDto>> GetPublicBySlugAsync(string slug, CancellationToken ct = default);
        /// <summary>Public: klik qeydi (səssiz — xəta istifadəçiyə qayıtmır).</summary>
        Task TrackClickAsync(string slug, int itemId, CancellationToken ct = default);
    }
}

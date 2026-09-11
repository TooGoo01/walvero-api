using Loyalty.Business.Dtos.Main;
using Loyalty.Business.Results;

namespace Loyalty.Business.Services.Abstractions.Main
{
    /// <summary>Menu-app kampaniya popup-larının idarəsi (tenant-scoped) + public oxu.</summary>
    public interface IMenuPopupService
    {
        Task<ServiceResult> ListAsync(int tenantId, CancellationToken ct = default);
        Task<ServiceResult> SaveAsync(int tenantId, SaveMenuPopupDto dto, CancellationToken ct = default);
        Task<ServiceResult> DeleteAsync(int tenantId, int id, CancellationToken ct = default);

        /// <summary>Tenant slug üzrə birinci aktiv popup (menu-app üçün). Yoxdursa null data.</summary>
        Task<ServiceResult> GetPublicAsync(string tenantSlug, CancellationToken ct = default);
    }
}

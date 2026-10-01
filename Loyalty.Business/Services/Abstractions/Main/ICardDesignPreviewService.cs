using Loyalty.Business.Results;

namespace Loyalty.Business.Services.Abstractions.Main
{
    /// <summary>
    /// Kart dizaynının publish-siz testi (Faza 0.3): SAXLANMIŞ dizaynla nümunə müştəri üçün
    /// Apple .pkpass (bytes, DB/push yoxdur) və Google preview class/obyekt (Save-to-Wallet linki). Real kartlara toxunulmur.
    /// </summary>
    public interface ICardDesignPreviewService
    {
        /// <summary>Data = byte[] (.pkpass). Serial PREVIEW-…, webServiceURL yoxdur.</summary>
        Task<ServiceResult> ApplePreviewAsync(int tenantId, int loyaltyProgramId, CancellationToken ct = default);

        /// <summary>Data = { url, classId, objectId, classStatus }. previewKey adətən admin user id-si (hər admin öz preview obyekti).</summary>
        Task<ServiceResult> GooglePreviewAsync(int tenantId, int loyaltyProgramId, string previewKey, CancellationToken ct = default);

        /// <summary>Real Google class vəziyyəti (read-only).</summary>
        Task<ServiceResult> GoogleClassStatusAsync(int tenantId, int loyaltyProgramId, CancellationToken ct = default);
    }
}

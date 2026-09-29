using Loyalty.Business.Dtos.Post.Main;
using Loyalty.Business.Results;

namespace Loyalty.Business.Services.Abstractions.Main
{
    /// <summary>
    /// Kənar sistemdən müştəri köçürməsi (admin). Yalnız DB: müştəri tap/yarat, kart tap/yarat, açılış balansı (Adjusted).
    /// POS (iiko) sinxronu ayrıca endpoint-lərlə edilir. SMS, pass push, welcome bonus yoxdur.
    /// </summary>
    public interface IPosCustomerImportService
    {
        Task<ServiceResult<ImportCustomerResultDto>> ImportAsync(int tenantId, ImportCustomerDto dto, bool dryRun, CancellationToken ct = default);
    }
}

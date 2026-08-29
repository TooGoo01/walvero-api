using Loyalty.Business.Results;

namespace Loyalty.Business.Services.Abstractions.Main
{
    public interface IKeywordGenerationService
    {
        /// <summary>Tenant üçün AI ilə axtarış açar sözləri generasiya et (ad+kateqoriya+menyu+təsvir).</summary>
        Task<ServiceResult> GenerateForTenantAsync(int tenantId, CancellationToken ct = default);
    }
}

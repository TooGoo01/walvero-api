using Loyalty.Business.Dtos.Post.Main.Branch;
using Loyalty.Business.Results;
using Microsoft.AspNetCore.Http;

namespace Loyalty.Business.Services.Abstractions.Main
{
    public interface IBranchService
    {
        Task<ServiceResult> ListAsync(int? tenantId);
        Task<ServiceResult> GetByIdAsync(int id);
        Task<ServiceResult> CreateAsync(CreateBranchDto dto, int tenantId);
        Task<ServiceResult> UpdateAsync(int id, UpdateBranchDto dto);
        Task<ServiceResult> DeleteAsync(int id);
        Task<ServiceResult> GetStatsAsync(int branchId);

        /// <summary>
        /// Branch foto/logo ??kli üçün generic upload — Azure Blob-a yaz?r, URL qaytar?r.
        /// DB-d? heç bir yaz? saxlan?lm?r; ça??ran t?r?f qaytar?lan URL-i CreateBranch/UpdateBranch
        /// DTO-sundak? PhotoUrl v? ya LogoUrl sah?sind? istifad? edir.
        /// </summary>
        Task<ServiceResult> UploadImageAsync(IFormFile file);
    }
}

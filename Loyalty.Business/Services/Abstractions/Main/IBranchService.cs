using Loyalty.Business.Dtos.Post.Main.Branch;
using Loyalty.Business.Results;
using Microsoft.AspNetCore.Http;

namespace Loyalty.Business.Services.Abstractions.Main
{
    public interface IBranchService
    {
        Task<ServiceResult> ListAsync(int? tenantId);
        // tenantId dolu gələndə ownership yoxlanılır — branch həmin tenant-a aid olmalıdır
        // (null = superadmin, yoxlama yoxdur).
        Task<ServiceResult> GetByIdAsync(int id, int? tenantId = null);
        Task<ServiceResult> CreateAsync(CreateBranchDto dto, int tenantId);
        Task<ServiceResult> UpdateAsync(int id, UpdateBranchDto dto, int? tenantId = null);
        Task<ServiceResult> DeleteAsync(int id, int? tenantId = null);
        Task<ServiceResult> GetStatsAsync(int branchId, int? tenantId = null);

        /// <summary>
        /// Branch foto/logo ??kli ���n generic upload � Azure Blob-a yaz?r, URL qaytar?r.
        /// DB-d? he� bir yaz? saxlan?lm?r; �a??ran t?r?f qaytar?lan URL-i CreateBranch/UpdateBranch
        /// DTO-sundak? PhotoUrl v? ya LogoUrl sah?sind? istifad? edir.
        /// </summary>
        Task<ServiceResult> UploadImageAsync(IFormFile file);

        /// <summary>
        /// Banner video upload — +faststart re-encode + poster generator + cache header-lər ilə yüklənir.
        /// `video-branch-banner` container-ə düşür. Qaytarır { videoUrl, videoFileName, posterUrl, posterFileName }.
        /// </summary>
        Task<ServiceResult> UploadBannerVideoAsync(IFormFile file);

        /// <summary>
        /// Operatorun (waiter/moderator) bağlı olduğu branchı tap. JWT-də branch_id yoxdursa
        /// DB-də ApplicationUser.BranchId üzərindən resolve edən fallback. Tapılmasa null.
        /// </summary>
        Task<int?> GetBranchIdByOperatorIdAsync(int operatorId, CancellationToken ct = default);
    }
}

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
        /// Branch foto/logo şəkli üçün generic upload — Azure Blob-a yazır, URL qaytarır.
        /// DB-də heç bir yazı saxlanılmır; çağıran tərəf qaytarılan URL-i CreateBranch/UpdateBranch
        /// DTO-sundakı PhotoUrl və ya LogoUrl sahəsində istifadə edir.
        /// </summary>
        Task<ServiceResult> UploadImageAsync(IFormFile file);
    }
}

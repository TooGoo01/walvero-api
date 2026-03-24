using Loyalty.Business.Results;

namespace Loyalty.Business.Services.Abstractions.Main
{
    public interface IBranchService
    {
        Task<ServiceResult> ListAsync(int tenantId, CancellationToken ct = default);
        Task<ServiceResult> GetByIdAsync(int id, CancellationToken ct = default);
        Task<ServiceResult> CreateAsync(int tenantId, Dtos.Post.Branch.CreateBranchDto dto, CancellationToken ct = default);
        Task<ServiceResult> UpdateAsync(int id, Dtos.Post.Branch.UpdateBranchDto dto, CancellationToken ct = default);
        Task<ServiceResult> DeleteAsync(int id, CancellationToken ct = default);
        Task<ServiceResult> GetNearbyAsync(int tenantId, double lat, double lng, double radiusKm, CancellationToken ct = default);
        Task<ServiceResult> GetStatsAsync(int branchId, CancellationToken ct = default);
    }
}

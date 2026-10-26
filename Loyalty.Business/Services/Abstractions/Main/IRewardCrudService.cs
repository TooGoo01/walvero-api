using Loyalty.Business.Results;

namespace Loyalty.Business.Services.Abstractions.Main
{
    public interface IRewardCrudService
    {
        Task<ServiceResult<RewardDto>> CreateAsync(RewardCreateDto dto, int? tenantId, CancellationToken ct = default);
        Task<ServiceResult<List<RewardDto>>> ListByProgramAsync(int loyaltyProgramId, int? tenantId, CancellationToken ct = default);
        Task<ServiceResult<RewardDto?>> GetByIdAsync(int id, int? tenantId, CancellationToken ct = default);
        Task<ServiceResult<bool>> UpdateAsync(int id, RewardUpdateDto dto, int? tenantId, CancellationToken ct = default);
        Task<ServiceResult<bool>> DeleteAsync(int id, int? tenantId, CancellationToken ct = default);
    }

    public class RewardDto
    {
        public int Id { get; set; }
        public int LoyaltyProgramId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public int PointsCost { get; set; }
        public string? ImageUrl { get; set; }
        public string? ExternalProductId { get; set; }
        public bool IsActive { get; set; }
        public int? StockQuantity { get; set; }
        /// <summary>Kassada bu hədiyyəni verən endirim növü.</summary>
        public string? PosDiscountTypeId { get; set; }
        /// <summary>Kassadan idxal olunub (iiko proqramı:qiymət).</summary>
        public string? PosSourceKey { get; set; }
        /// <summary>Hədiyyə ola bilən kassa məhsulları.</summary>
        public List<RewardPosProductDto> PosProducts { get; set; } = new();
    }

    public sealed record RewardPosProductDto(string Code, string? Name);

    public class RewardCreateDto
    {
        public int LoyaltyProgramId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public int PointsCost { get; set; }
        public string? ImageUrl { get; set; }
        public string? ExternalProductId { get; set; }
        public int? StockQuantity { get; set; }
        public string? PosDiscountTypeId { get; set; }
        public List<RewardPosProductDto>? PosProducts { get; set; }
    }

    public class RewardUpdateDto
    {
        public string? Name { get; set; }
        public string? Description { get; set; }
        public int? PointsCost { get; set; }
        public string? ImageUrl { get; set; }
        public string? ExternalProductId { get; set; }
        public bool? IsActive { get; set; }
        public int? StockQuantity { get; set; }
        /// <summary>null = dəyişmə, "" = sil.</summary>
        public string? PosDiscountTypeId { get; set; }
        /// <summary>null = dəyişmə; siyahı = tam əvəz (boş siyahı = hamısını sil).</summary>
        public List<RewardPosProductDto>? PosProducts { get; set; }
    }
}

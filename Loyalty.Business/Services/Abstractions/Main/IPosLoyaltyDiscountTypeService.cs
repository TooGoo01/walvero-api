using Loyalty.Business.Results;

namespace Loyalty.Business.Services.Abstractions.Main
{
    public interface IPosLoyaltyDiscountTypeService
    {
        Task<ServiceResult<PosLoyaltyDiscountTypeDto>> CreateAsync(PosLoyaltyDiscountTypeCreateDto dto, int? tenantId, CancellationToken ct = default);
        Task<ServiceResult<List<PosLoyaltyDiscountTypeDto>>> ListByTenantIntegrationAsync(int tenantIntegrationId, int? tenantId, CancellationToken ct = default);
        Task<ServiceResult<bool>> UpdateAsync(int id, PosLoyaltyDiscountTypeUpdateDto dto, int? tenantId, CancellationToken ct = default);
        Task<ServiceResult<bool>> DeleteAsync(int id, int? tenantId, CancellationToken ct = default);
    }

    public class PosLoyaltyDiscountTypeDto
    {
        public int Id { get; set; }
        public int TenantIntegrationId { get; set; }
        public string ExternalDiscountTypeId { get; set; } = string.Empty;
        public string? DiscountName { get; set; }
        public bool IsActive { get; set; }
    }

    public class PosLoyaltyDiscountTypeCreateDto
    {
        public int TenantIntegrationId { get; set; }
        public string ExternalDiscountTypeId { get; set; } = string.Empty;
        public string? DiscountName { get; set; }
    }

    public class PosLoyaltyDiscountTypeUpdateDto
    {
        public string? ExternalDiscountTypeId { get; set; }
        public string? DiscountName { get; set; }
        public bool? IsActive { get; set; }
    }
}

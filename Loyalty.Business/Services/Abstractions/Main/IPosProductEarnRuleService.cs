using Loyalty.Business.Results;

namespace Loyalty.Business.Services.Abstractions.Main
{
    public interface IPosProductEarnRuleService
    {
        Task<ServiceResult<PosProductEarnRuleDto>> CreateAsync(PosProductEarnRuleCreateDto dto, int? tenantId, CancellationToken ct = default);
        Task<ServiceResult<List<PosProductEarnRuleDto>>> ListByTenantIntegrationAsync(int tenantIntegrationId, int? tenantId, CancellationToken ct = default);
        Task<ServiceResult<bool>> UpdateAsync(int id, PosProductEarnRuleUpdateDto dto, int? tenantId, CancellationToken ct = default);
        Task<ServiceResult<bool>> DeleteAsync(int id, int? tenantId, CancellationToken ct = default);
    }

    public class PosProductEarnRuleDto
    {
        public int Id { get; set; }
        public int TenantIntegrationId { get; set; }
        public string ExternalProductId { get; set; } = string.Empty;
        public string? ProductName { get; set; }
        public int StepsPerUnit { get; set; }
        public int PointsPerUnit { get; set; }
        /// <summary>null = inteqrasiyanın bütün proqramları; dolu = yalnız həmin proqram (məs. ambassador).</summary>
        public int? LoyaltyProgramId { get; set; }
    }

    public class PosProductEarnRuleCreateDto
    {
        public int TenantIntegrationId { get; set; }
        public string ExternalProductId { get; set; } = string.Empty;
        public string? ProductName { get; set; }
        public int StepsPerUnit { get; set; }
        public int PointsPerUnit { get; set; }
        public int? LoyaltyProgramId { get; set; }
    }

    public class PosProductEarnRuleUpdateDto
    {
        public string? ExternalProductId { get; set; }
        public string? ProductName { get; set; }
        public int? StepsPerUnit { get; set; }
        public int? PointsPerUnit { get; set; }
        /// <summary>null = dəyişmə; 0 = bütün proqramlar; &gt;0 = həmin proqram.</summary>
        public int? LoyaltyProgramId { get; set; }
    }
}

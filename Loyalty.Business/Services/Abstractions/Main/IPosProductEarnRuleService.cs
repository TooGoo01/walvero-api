using Loyalty.Business.Results;
using Loyalty.DataAccess.Entities.Main;

namespace Loyalty.Business.Services.Abstractions.Main
{
    public interface IPosProductEarnRuleService
    {
        Task<ServiceResult<PosProductEarnRuleDto>> CreateAsync(PosProductEarnRuleCreateDto dto, int? tenantId, CancellationToken ct = default);
        Task<ServiceResult<List<PosProductEarnRuleDto>>> ListByTenantIntegrationAsync(int tenantIntegrationId, int? tenantId, CancellationToken ct = default);
        Task<ServiceResult<bool>> UpdateAsync(int id, PosProductEarnRuleUpdateDto dto, int? tenantId, CancellationToken ct = default);
        Task<ServiceResult<bool>> DeleteAsync(int id, int? tenantId, CancellationToken ct = default);
        /// <summary>Toplu əlavə/yeniləmə: açar (proqram, növ, ExternalProductId) — mövcuddursa dəyərlər yenilənir.</summary>
        Task<ServiceResult<PosProductEarnRuleBulkResultDto>> BulkUpsertAsync(int tenantIntegrationId, List<PosProductEarnRuleCreateDto> items, int? tenantId, CancellationToken ct = default);
    }

    public class PosProductEarnRuleDto
    {
        public int Id { get; set; }
        public int TenantIntegrationId { get; set; }
        /// <summary>1 = Product, 2 = Group, 3 = Category.</summary>
        public PosEarnExclusionKind Kind { get; set; } = PosEarnExclusionKind.Product;
        public string ExternalProductId { get; set; } = string.Empty;
        public string? Code { get; set; }
        public string? ProductName { get; set; }
        public int StepsPerUnit { get; set; }
        public int PointsPerUnit { get; set; }
        /// <summary>null = inteqrasiyanın bütün proqramları; dolu = yalnız həmin proqram (məs. ambassador).</summary>
        public int? LoyaltyProgramId { get; set; }
    }

    public class PosProductEarnRuleCreateDto
    {
        public int TenantIntegrationId { get; set; }
        /// <summary>1 = Product (default), 2 = Group, 3 = Category.</summary>
        public PosEarnExclusionKind Kind { get; set; } = PosEarnExclusionKind.Product;
        public string ExternalProductId { get; set; } = string.Empty;
        public string? Code { get; set; }
        public string? ProductName { get; set; }
        public int StepsPerUnit { get; set; }
        public int PointsPerUnit { get; set; }
        public int? LoyaltyProgramId { get; set; }
    }

    public class PosProductEarnRuleUpdateDto
    {
        public string? ExternalProductId { get; set; }
        public string? Code { get; set; }
        public string? ProductName { get; set; }
        public int? StepsPerUnit { get; set; }
        public int? PointsPerUnit { get; set; }
        /// <summary>null = dəyişmə; 0 = bütün proqramlar; &gt;0 = həmin proqram.</summary>
        public int? LoyaltyProgramId { get; set; }
    }

    public class PosProductEarnRuleBulkResultDto
    {
        public int Created { get; set; }
        public int Updated { get; set; }
        public int Skipped { get; set; }
    }
}

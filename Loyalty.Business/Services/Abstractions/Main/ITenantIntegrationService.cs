using Loyalty.Business.Results;

namespace Loyalty.Business.Services.Abstractions.Main
{
    public interface ITenantIntegrationService
    {
        Task<ServiceResult<TenantIntegrationCreateResult>> CreateAsync(TenantIntegrationCreateDto dto, CancellationToken ct = default);
        Task<ServiceResult<List<TenantIntegrationListDto>>> ListByTenantAsync(int tenantId, CancellationToken ct = default);
        Task<ServiceResult<TenantIntegrationDetailDto?>> GetByIdAsync(int id, int? tenantId, CancellationToken ct = default);
        Task<ServiceResult<bool>> UpdateAsync(int id, TenantIntegrationUpdateDto dto, int? tenantId, CancellationToken ct = default);
        Task<ServiceResult<bool>> DeleteAsync(int id, int? tenantId, CancellationToken ct = default);
        Task<ServiceResult<string?>> RegenerateWalletTokenAsync(int id, int? tenantId, CancellationToken ct = default);

        // Per-branch venues (iiko multi-org)
        Task<ServiceResult<List<TenantIntegrationVenueDto>>> ListVenuesAsync(int integrationId, int? tenantId, CancellationToken ct = default);
        Task<ServiceResult<int>> AddVenueAsync(int integrationId, TenantIntegrationVenueUpsertDto dto, int? tenantId, CancellationToken ct = default);
        Task<ServiceResult<bool>> UpdateVenueAsync(int venueId, TenantIntegrationVenueUpsertDto dto, int? tenantId, CancellationToken ct = default);
        Task<ServiceResult<bool>> DeleteVenueAsync(int venueId, int? tenantId, CancellationToken ct = default);
        /// <summary>Live-fetch iiko organizations + terminal groups using the integration's stored credentials.</summary>
        Task<ServiceResult<List<IikoOrgOptionDto>>> GetIikoOrganizationsAsync(int integrationId, int? tenantId, CancellationToken ct = default);
    }

    public class TenantIntegrationCreateDto
    {
        public int TenantId { get; set; }
        public int IntegrationTypeId { get; set; }
        public string? DisplayName { get; set; }
        public string? ExternalVenueId { get; set; }
        public string? ExternalProgramId { get; set; }
        public string? ExternalTerminalGroupId { get; set; }
        public int? LoyaltyProgramId { get; set; }
        public string? ApiBaseUrl { get; set; }
        public string? ApiUsername { get; set; }
        public string? ApiToken { get; set; }
        public string? ApiAppId { get; set; }
        public string? ApiSecret { get; set; }
    }

    public class TenantIntegrationCreateResult
    {
        public int Id { get; set; }
        public string WalletToken { get; set; } = string.Empty;
        public string WalletWebhookBaseUrl { get; set; } = string.Empty;
    }

    public class TenantIntegrationListDto
    {
        public int Id { get; set; }
        public int TenantId { get; set; }
        public string IntegrationTypeName { get; set; } = string.Empty;
        public string Source { get; set; } = string.Empty;
        public string? DisplayName { get; set; }
        public string? ExternalVenueId { get; set; }
        public bool IsActive { get; set; }
    }

    public class TenantIntegrationDetailDto
    {
        public int Id { get; set; }
        public int TenantId { get; set; }
        public int IntegrationTypeId { get; set; }
        public string IntegrationTypeName { get; set; } = string.Empty;
        public string Source { get; set; } = string.Empty;
        public string? DisplayName { get; set; }
        public string? ExternalVenueId { get; set; }
        public string? ExternalProgramId { get; set; }
        public string? ExternalTerminalGroupId { get; set; }
        public int? LoyaltyProgramId { get; set; }
        public string? ApiBaseUrl { get; set; }
        public string? ApiUsername { get; set; }
        public string? ApiAppId { get; set; }
        public bool IsActive { get; set; }
    }

    public class TenantIntegrationUpdateDto
    {
        public string? DisplayName { get; set; }
        public string? ExternalVenueId { get; set; }
        public string? ExternalProgramId { get; set; }
        public string? ExternalTerminalGroupId { get; set; }
        public int? LoyaltyProgramId { get; set; }
        public string? ApiBaseUrl { get; set; }
        public string? ApiUsername { get; set; }
        public string? ApiToken { get; set; }
        public string? ApiAppId { get; set; }
        public string? ApiSecret { get; set; }
        public bool? IsActive { get; set; }
    }

    public class TenantIntegrationVenueDto
    {
        public int Id { get; set; }
        public int TenantIntegrationId { get; set; }
        public int? BranchId { get; set; }
        public string? BranchName { get; set; }
        public string ExternalVenueId { get; set; } = string.Empty;
        public string? ExternalTerminalGroupId { get; set; }
        public string? ExternalProgramId { get; set; }
        public string? DisplayName { get; set; }
        public bool IsActive { get; set; }
    }

    public class TenantIntegrationVenueUpsertDto
    {
        public int? BranchId { get; set; }
        public string? ExternalVenueId { get; set; }
        public string? ExternalTerminalGroupId { get; set; }
        public string? ExternalProgramId { get; set; }
        public string? DisplayName { get; set; }
        public bool? IsActive { get; set; }
    }

    public class IikoOrgOptionDto
    {
        public string OrganizationId { get; set; } = string.Empty;
        public string? Name { get; set; }
        public List<IikoTerminalGroupOptionDto> TerminalGroups { get; set; } = new();
    }

    public class IikoTerminalGroupOptionDto
    {
        public string Id { get; set; } = string.Empty;
        public string? Name { get; set; }
    }
}

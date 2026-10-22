using Loyalty.Business.Helpers;
using Loyalty.Business.Results;
using Loyalty.DataAccess.Enums;

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
        /// <summary>ExternalProgramId boş venue-ları iiko-dan avtomatik doldurur. Doldurulan venue sayını qaytarır.</summary>
        Task<ServiceResult<int>> AutoFillVenueProgramsAsync(int integrationId, int? tenantId, CancellationToken ct = default);
        Task<ServiceResult<RestampBranchesResultDto>> RestampIikoTransactionBranchesAsync(int integrationId, RestampBranchesDto dto, CancellationToken ct = default);
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
        /// <summary>iiko əsas kart track rejimi (1 tam barkod, 2 Walvero ID, 3 son N rəqəm). Boş = tam barkod.</summary>
        public PosCardTrackMode? CardTrackMode { get; set; }
        /// <summary>LastDigits üçün N (4–8).</summary>
        public int? CardTrackDigits { get; set; }
        /// <summary>iiko-ya telefon göndərilsin (default true).</summary>
        public bool? SendPhoneToPos { get; set; }
        /// <summary>İlk göndərişdə telefonla mövcud iiko qonağını tap + köhnə balansını Walvero-ya əlavə et (default false).</summary>
        public bool? MatchExistingByPhone { get; set; }
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
        public PosCardTrackMode CardTrackMode { get; set; }
        public int? CardTrackDigits { get; set; }
        public string CardTrackLabel { get; set; } = string.Empty;
        public int? LoyaltyProgramId { get; set; }
        public string? AmbassadorIikoCategoryId { get; set; }
        public bool SendPhoneToPos { get; set; }
        public bool MatchExistingByPhone { get; set; }
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
        public PosCardTrackMode CardTrackMode { get; set; }
        public int? CardTrackDigits { get; set; }
        public string CardTrackLabel { get; set; } = string.Empty;
        public string? AmbassadorIikoCategoryId { get; set; }
        public bool SendPhoneToPos { get; set; }
        public bool MatchExistingByPhone { get; set; }
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
        /// <summary>iiko əsas kart track rejimi; yalnız super admin dəyişir (controller yoxlayır).</summary>
        public PosCardTrackMode? CardTrackMode { get; set; }
        public int? CardTrackDigits { get; set; }
        /// <summary>iiko ambassador qonaq kateqoriyası (GUID). null = dəyişmə; "" = sil.</summary>
        public string? AmbassadorIikoCategoryId { get; set; }
        /// <summary>Telefon flag-ləri; yalnız super admin dəyişir (controller yoxlayır). null = dəyişmə.</summary>
        public bool? SendPhoneToPos { get; set; }
        public bool? MatchExistingByPhone { get; set; }
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

    /// <summary>Tarixi iiko tranzaksiyalarının BranchId-sini xəritə ilə dəyiş (venue filial səhvi düzəlişi).</summary>
    public class RestampBranchesDto
    {
        public List<BranchRemapItem> Map { get; set; } = new();
        /// <summary>Bu andan əvvəl yaranan tx-lər; null → integration venue-larının ən erkən EditDate-i.</summary>
        public DateTime? Before { get; set; }
        public bool DryRun { get; set; } = true;
    }

    public class BranchRemapItem
    {
        public int FromBranchId { get; set; }
        public int ToBranchId { get; set; }
    }

    public class RestampBranchesResultDto
    {
        public DateTime Before { get; set; }
        public bool DryRun { get; set; }
        public Dictionary<string, int> Counts { get; set; } = new();
        public int Updated { get; set; }
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
        public List<IikoProgramOptionDto> Programs { get; set; } = new();
    }

    public class IikoTerminalGroupOptionDto
    {
        public string Id { get; set; } = string.Empty;
        public string? Name { get; set; }
    }

    public class IikoProgramOptionDto
    {
        public string Id { get; set; } = string.Empty;
        public string? Name { get; set; }
    }
}

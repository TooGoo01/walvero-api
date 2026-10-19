using Loyalty.Business.Helpers;
using Loyalty.Business.Results;
using Loyalty.DataAccess.Entities.Main;

namespace Loyalty.Business.Services.Abstractions.Main
{
    /// <summary>iiko inteqrasiyası üçün token + org (venue) siyahısı — yeni iiko kodu üçün ortaq giriş nöqtəsi.</summary>
    public interface IIikoConnectionResolver
    {
        /// <summary>Tenant-a aid aktiv iiko inteqrasiyası; başqa tenant-ınkı və ya iiko deyilsə səhv.</summary>
        Task<(TenantIntegration? Integration, string? Error)> GetOwnedIikoIntegrationAsync(int tenantId, int integrationId, CancellationToken ct = default);
        /// <summary>Org-lar: aktiv venue-lar (filial adı ilə), yoxdursa legacy ExternalVenueId.</summary>
        Task<List<IikoOrgTarget>> GetOrgsAsync(TenantIntegration integration, CancellationToken ct = default);
        Task<(string? Token, string? Error)> GetTokenAsync(TenantIntegration integration, CancellationToken ct = default);
    }

    public sealed class IikoOrgTarget
    {
        public int? VenueId { get; set; }
        public int? BranchId { get; set; }
        public string? BranchName { get; set; }
        public string? DisplayName { get; set; }
        public string OrganizationId { get; set; } = string.Empty;
    }

    /// <summary>
    /// iiko kataloqu — proqramlar, cüzdanlar, qonaq kateqoriyaları, nomenklatura bir yerdə (tenant admin + super admin).
    /// Seçim siyahıları, idxal və sinxron paneli bunu oxuyur.
    /// </summary>
    public interface IIikoCatalogService
    {
        Task<ServiceResult<IikoCatalogDto>> GetCatalogAsync(int tenantId, int integrationId, bool refresh, CancellationToken ct = default);
        /// <summary>Kassa kampaniyalarından Walvero ayar təklifləri (önbaxış, heç nə yazılmır).</summary>
        Task<ServiceResult<IikoImportPreviewDto>> PreviewImportAsync(int tenantId, int integrationId, CancellationToken ct = default);
        /// <summary>Seçilmiş təklifləri tətbiq et (önbaxış yenidən hesablanır — köhnə ekran vəziyyəti tətbiq olunmur).</summary>
        Task<ServiceResult<IikoImportResultDto>> ApplyImportAsync(int tenantId, int integrationId, List<string> keys, int userId, CancellationToken ct = default);
        /// <summary>Kassa ↔ Walvero sinxron vəziyyəti (bağlı/bağlanmamış cüzdanlar, riskli rejimlər, kateqoriya, kataloq).</summary>
        Task<ServiceResult<IikoSyncStatusDto>> GetSyncStatusAsync(int tenantId, int integrationId, CancellationToken ct = default);
    }

    public class IikoImportProposalDto
    {
        public string Key { get; set; } = string.Empty;
        /// <summary>cashback | unit_payment | info | unsupported</summary>
        public string Kind { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Explanation { get; set; } = string.Empty;
        public List<IikoImportChangeDto> Changes { get; set; } = new();
        public bool Applicable { get; set; }
        /// <summary>Bütün dəyişikliklər artıq tətbiq olunub.</summary>
        public bool AlreadyApplied { get; set; }
        public bool SelectedByDefault { get; set; }
        public string? Warning { get; set; }
        /// <summary>Eyni qrupun təklifləri alternativdir (məs. keşbeki kassa və ya Walvero hesablasın) — yalnız biri seçilə bilər.</summary>
        public string? Group { get; set; }
    }

    /// <param name="Target">program | new_ambassador_program | template | integration</param>
    public sealed record IikoImportChangeDto(string Target, int? ProgramId, string TargetName, string Field, string? From, string? To);

    public class IikoImportPreviewDto
    {
        public List<IikoImportProposalDto> Proposals { get; set; } = new();
        public List<string> Errors { get; set; } = new();
    }

    public class IikoImportResultDto
    {
        public List<string> Applied { get; set; } = new();
        public List<string> Skipped { get; set; } = new();
        public int? CreatedProgramId { get; set; }
    }

    /// <param name="Level">ok | info | warn | error</param>
    /// <param name="Area">wallet | program | category | venue | nomenclature | rules</param>
    public sealed record IikoSyncItemDto(string Level, string Area, string Text, string? Hint);

    public class IikoSyncStatusDto
    {
        public DateTime GeneratedAtUtc { get; set; }
        /// <summary>Ən pis səviyyə (kart nişanı üçün).</summary>
        public string Level { get; set; } = "ok";
        public List<IikoSyncItemDto> Items { get; set; } = new();
    }

    public class IikoCatalogDto
    {
        public int IntegrationId { get; set; }
        public string? DisplayName { get; set; }
        public DateTime GeneratedAtUtc { get; set; }
        public List<IikoCatalogVenueDto> Venues { get; set; } = new();
        public List<IikoCatalogProgramDto> Programs { get; set; } = new();
        public List<IikoCatalogWalletDto> Wallets { get; set; } = new();
        public List<IikoCatalogGuestCategoryDto> GuestCategories { get; set; } = new();
        /// <summary>Məhsul qrupları / kateqoriyaları (PosNomenclatureCache + sifarişlərdən yığılan məhsullar).</summary>
        public PosNomenclatureDto? Nomenclature { get; set; }
        public List<string> Errors { get; set; } = new();
    }

    public class IikoCatalogVenueDto
    {
        public int? VenueId { get; set; }
        public string? BranchName { get; set; }
        public string? DisplayName { get; set; }
        public string OrganizationId { get; set; } = string.Empty;
        public string? Error { get; set; }
        public DateTime FetchedAtUtc { get; set; }
        public bool FromCache { get; set; }
    }

    public class IikoCatalogProgramDto
    {
        public string Id { get; set; } = string.Empty;
        public string? Name { get; set; }
        public bool IsActive { get; set; }
        public string? WalletId { get; set; }
        public decimal? CashbackPercent { get; set; }
        /// <summary>Proqramın göründüyü filiallar (venue adları).</summary>
        public List<string> Venues { get; set; } = new();
        public List<IikoCampaignSummary> Campaigns { get; set; } = new();
    }

    public class IikoCatalogWalletDto
    {
        public string Id { get; set; } = string.Empty;
        /// <summary>Oxunaqlı ad — cüzdana bağlı proqramın adı (iiko cüzdan adını ayrıca vermir).</summary>
        public string? Name { get; set; }
        public string? ProgramId { get; set; }
        public string? ProgramName { get; set; }
        /// <summary>Bu cüzdanı işlədən kampaniyalar ("Поплнение (Keşbek %)" …).</summary>
        public List<string> UsedBy { get; set; } = new();
        /// <summary>Cüzdandan ödəniş kampaniyası var (PaymentAction).</summary>
        public bool HasPayment { get; set; }
        /// <summary>Ödəniş məhsul başına vahidlədir (UnitsPrice) — balans = məhsul sayı.</summary>
        public decimal? UnitsPerItem { get; set; }
        /// <summary>Kampaniyalar yalnız bu qonaq kateqoriyalarına işləyir (məs. ambassador).</summary>
        public List<string> GuestCategoryIds { get; set; } = new();
    }

    public class IikoCatalogGuestCategoryDto
    {
        public string Id { get; set; } = string.Empty;
        public string? Name { get; set; }
        public bool IsActive { get; set; }
        public bool IsDefaultForNewGuests { get; set; }
        /// <summary>Bu kateqoriyanı şərt kimi işlədən kampaniyalar.</summary>
        public List<string> UsedBy { get; set; } = new();
    }
}

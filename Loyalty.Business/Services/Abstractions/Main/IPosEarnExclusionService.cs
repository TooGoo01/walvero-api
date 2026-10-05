using Loyalty.Business.Helpers;
using Loyalty.Business.Results;
using Loyalty.DataAccess.Entities.Main;

namespace Loyalty.Business.Services.Abstractions.Main
{
    /// <summary>Sifariş emalı üçün: inteqrasiyanın bonus istisna xəritəsi (DB + nomenklatura surəti, yaddaş keşi).</summary>
    public interface IEarnExclusionResolver
    {
        Task<EarnExclusionMap> GetMapAsync(int tenantIntegrationId, CancellationToken ct = default);
    }

    /// <summary>Super admin: bonus qazandırmayan məhsul/qrup/kateqoriya idarəsi + iiko nomenklaturası.</summary>
    public interface IPosEarnExclusionService
    {
        Task<ServiceResult<List<PosEarnExclusionDto>>> ListAsync(int tenantIntegrationId, CancellationToken ct = default);
        Task<ServiceResult<List<PosEarnExclusionDto>>> UpsertAsync(int tenantIntegrationId, List<PosEarnExclusionCreateDto> items, int userId, CancellationToken ct = default);
        Task<ServiceResult<bool>> UpdateAsync(int tenantIntegrationId, int id, PosEarnExclusionUpdateDto dto, int userId, CancellationToken ct = default);
        Task<ServiceResult<bool>> DeleteAsync(int tenantIntegrationId, int id, CancellationToken ct = default);

        Task<ServiceResult<PosNomenclatureDto>> GetNomenclatureAsync(int tenantIntegrationId, CancellationToken ct = default);
        Task<ServiceResult<List<PosNomenclatureProductDto>>> SearchProductsAsync(int tenantIntegrationId, string? q, string? groupId, string? categoryId, int take, CancellationToken ct = default);
        Task<ServiceResult<PosNomenclatureRefreshResultDto>> RefreshNomenclatureAsync(int tenantIntegrationId, CancellationToken ct = default);

        Task<ServiceResult<EarnExclusionSimulateResultDto>> SimulateAsync(int tenantIntegrationId, EarnExclusionSimulateRequestDto request, CancellationToken ct = default);
    }

    /// <summary>
    /// Sifarişdə görünən məhsulları (ID, kod, ad, kateqoriya) inteqrasiya siyahısına yazır — iiko Cloud nomenklaturası
    /// boş olan restoranlarda picker bu siyahıdan işləyir. Yalnız yeni/dəyişmiş məhsul DB-yə düşür (yaddaş keşi).
    /// </summary>
    public interface IPosObservedProductRecorder
    {
        Task RecordAsync(int tenantIntegrationId, IReadOnlyList<IikoOrderItem> items, CancellationToken ct = default);
    }

    /// <summary>Hangfire: istisnası olan iiko inteqrasiyalarının nomenklaturasını yenilə.</summary>
    public interface IPosNomenclatureJobs
    {
        Task RefreshAllAsync(CancellationToken ct = default);
    }

    public class PosEarnExclusionDto
    {
        public int Id { get; set; }
        public int TenantIntegrationId { get; set; }
        public PosEarnExclusionKind Kind { get; set; }
        public string ExternalId { get; set; } = string.Empty;
        public string? Code { get; set; }
        public string? Name { get; set; }
        public bool BlockCashback { get; set; }
        public bool BlockStamps { get; set; }
        public string? Note { get; set; }
        public bool IsActive { get; set; }
        public DateTime RegDate { get; set; }
        /// <summary>Qrup/kateqoriya üçün nomenklaturada əhatə olunan məhsul sayı (surət varsa).</summary>
        public int? ProductCount { get; set; }
        /// <summary>Qrup üçün nomenklaturadakı yol ("Bar / Alkoqol").</summary>
        public string? Path { get; set; }
    }

    public class PosEarnExclusionCreateDto
    {
        public PosEarnExclusionKind Kind { get; set; }
        public string ExternalId { get; set; } = string.Empty;
        public string? Code { get; set; }
        public string? Name { get; set; }
        public bool BlockCashback { get; set; } = true;
        public bool BlockStamps { get; set; } = true;
        public string? Note { get; set; }
    }

    public class PosEarnExclusionUpdateDto
    {
        public bool? BlockCashback { get; set; }
        public bool? BlockStamps { get; set; }
        public string? Note { get; set; }
        public bool? IsActive { get; set; }
    }

    public class PosNomenclatureDto
    {
        public DateTime? FetchedAtUtc { get; set; }
        public int OrganizationCount { get; set; }
        /// <summary>iiko Cloud nomenklaturasındakı məhsul sayı.</summary>
        public int ProductCount { get; set; }
        /// <summary>Sifarişlərdən yığılan məhsul sayı.</summary>
        public int ObservedProductCount { get; set; }
        public DateTime? ObservedLastSeenAtUtc { get; set; }
        public List<PosNomenclatureGroupDto> Groups { get; set; } = new();
        public List<PosNomenclatureCategoryDto> Categories { get; set; } = new();
    }

    public class PosNomenclatureGroupDto
    {
        public string Id { get; set; } = string.Empty;
        public string? Name { get; set; }
        public string? ParentGroup { get; set; }
        /// <summary>Alt-qruplar daxil məhsul sayı.</summary>
        public int ProductCount { get; set; }
    }

    public class PosNomenclatureCategoryDto
    {
        public string Id { get; set; } = string.Empty;
        public string? Name { get; set; }
        public int ProductCount { get; set; }
    }

    public class PosNomenclatureProductDto
    {
        public string Id { get; set; } = string.Empty;
        public string? Code { get; set; }
        public string? Name { get; set; }
        public string? Type { get; set; }
        public string? GroupId { get; set; }
        public string? GroupName { get; set; }
        public string? CategoryId { get; set; }
        public string? CategoryName { get; set; }
        /// <summary>"iiko" (Cloud nomenklaturası) | "orders" (sifarişlərdən yığılıb).</summary>
        public string Source { get; set; } = "iiko";
        public DateTime? LastSeenAtUtc { get; set; }
        /// <summary>Effektiv qayda (ağ siyahı daxil); null = bonus normal.</summary>
        public EarnBlockDto? Effective { get; set; }
    }

    public class EarnBlockDto
    {
        public bool BlockCashback { get; set; }
        public bool BlockStamps { get; set; }
        public PosEarnExclusionKind SourceKind { get; set; }
        public string SourceName { get; set; } = string.Empty;

        public static EarnBlockDto? From(EarnBlock? b) => b == null ? null : new EarnBlockDto
        {
            BlockCashback = b.BlockCashback,
            BlockStamps = b.BlockStamps,
            SourceKind = b.SourceKind,
            SourceName = b.SourceName
        };
    }

    public class PosNomenclatureRefreshResultDto
    {
        public int Organizations { get; set; }
        public int FailedOrganizations { get; set; }
        public int Products { get; set; }
        public int Groups { get; set; }
        public int Categories { get; set; }
        public DateTime FetchedAtUtc { get; set; }
    }

    public class EarnExclusionSimulateRequestDto
    {
        public List<EarnExclusionSimulateItemDto> Items { get; set; } = new();
        /// <summary>Boşdursa = Σ Sum − Σ DiscountSum.</summary>
        public decimal? TotalSum { get; set; }
        /// <summary>Wallet (bonusla) ödənilən hissə.</summary>
        public decimal WalletSum { get; set; }
        /// <summary>Gözlənilən cashback hesabı üçün faiz (UI-dan).</summary>
        public decimal? CashbackPercent { get; set; }
        public bool IsProgress { get; set; }
    }

    public class EarnExclusionSimulateItemDto
    {
        public string? ProductId { get; set; }
        public string? ProductNumber { get; set; }
        public string? CategoryId { get; set; }
        public string? Name { get; set; }
        public decimal Amount { get; set; } = 1;
        public decimal Sum { get; set; }
        /// <summary>Bu sətrə düşən endirim (DiscountByItem).</summary>
        public decimal DiscountSum { get; set; }
    }

    public class EarnExclusionSimulateResultDto
    {
        public decimal TotalSum { get; set; }
        /// <summary>İstisnasız köhnə baza.</summary>
        public decimal EarnBase { get; set; }
        public decimal CashbackBase { get; set; }
        public decimal ExcludedNet { get; set; }
        public decimal? ExpectedCashback { get; set; }
        public decimal? ExpectedCashbackWithoutExclusions { get; set; }
        public int Steps { get; set; }
        public decimal ProductPoints { get; set; }
        public bool AnyEarnRuleMatched { get; set; }
        public decimal StampSkippedUnits { get; set; }
        public List<EarnExclusionSimulateLineDto> Lines { get; set; } = new();
    }

    public class EarnExclusionSimulateLineDto
    {
        public string? ProductId { get; set; }
        public string? Name { get; set; }
        public decimal Amount { get; set; }
        public decimal Sum { get; set; }
        public bool CashbackBlocked { get; set; }
        public bool StampsBlocked { get; set; }
        public string? Reason { get; set; }
        public decimal ExcludedNet { get; set; }
        public int? StepsPerUnit { get; set; }
        public decimal? PointsPerUnit { get; set; }
    }
}

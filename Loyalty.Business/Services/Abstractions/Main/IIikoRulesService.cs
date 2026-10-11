using Loyalty.Business.Helpers;
using Loyalty.Business.Results;
using Loyalty.DataAccess.Enums;

namespace Loyalty.Business.Services.Abstractions.Main
{
    /// <summary>
    /// iiko loyallıq proqramının qaydalarını (kampaniyalar: hansı məhsula neçə %, şərtlər) tenant admin üçün
    /// read-only göstərir — iiko-dan canlı çəkilir, org (filial) üzrə, qısa müddət cache-lənir.
    /// </summary>
    public interface IIikoRulesService
    {
        Task<ServiceResult<IikoRulesResultDto>> GetRulesAsync(int tenantId, int integrationId, int? venueId, bool refresh, CancellationToken ct = default);
    }

    public class IikoRulesResultDto
    {
        public int IntegrationId { get; set; }
        public string? DisplayName { get; set; }
        public PosCardTrackMode CardTrackMode { get; set; }
        public int? CardTrackDigits { get; set; }
        /// <summary>"Tam barkod" / "Walvero ID" / "Son 6 rəqəm".</summary>
        public string CardTrackLabel { get; set; } = string.Empty;
        public DateTime GeneratedAtUtc { get; set; }
        public List<IikoRulesVenueDto> Venues { get; set; } = new();
        public IikoRulesWalveroDto Walvero { get; set; } = new();
    }

    public class IikoRulesVenueDto
    {
        /// <summary>TenantIntegrationVenue.Id; legacy tək-org inteqrasiyada null.</summary>
        public int? VenueId { get; set; }
        public int? BranchId { get; set; }
        public string? BranchName { get; set; }
        public string? DisplayName { get; set; }
        public string OrganizationId { get; set; } = string.Empty;
        /// <summary>iiko cavab vermədi / token yoxdur və s. — proqramlar boş.</summary>
        public string? Error { get; set; }
        public DateTime FetchedAtUtc { get; set; }
        public bool FromCache { get; set; }
        public List<IikoProgramSummary> Programs { get; set; } = new();
        /// <summary>Aktiv proqramın şərtsiz keşbek faizi (varsa).</summary>
        public decimal? CashbackPercent { get; set; }
        /// <summary>iiko nomenklaturasındakı məhsul kateqoriyaları (ad həlli + "hansı kateqoriyalar var" göstərişi).</summary>
        public List<IikoRulesNamedDto> Categories { get; set; } = new();
        public int ProductCount { get; set; }
    }

    public class IikoRulesNamedDto
    {
        public string Id { get; set; } = string.Empty;
        public string? Name { get; set; }
    }

    /// <summary>Walvero tərəfində eyni inteqrasiya üçün qoyulmuş istisnalar/qaydalar (müqayisə üçün, read-only).</summary>
    public class IikoRulesWalveroDto
    {
        public List<PosEarnExclusionDto> Exclusions { get; set; } = new();
        public int EarnRulesCount { get; set; }
    }
}

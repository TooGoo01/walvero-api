using System.Text.Json.Serialization;

namespace Loyalty.Business.Dtos.Get.Main
{
    /// <summary>
    /// Defines the mode of the wallet card details response.
    /// </summary>
    public enum WalletCardDetailsMode
    {
        /// <summary>
        /// Only tenant-level public information is returned (no card-specific data).
        /// </summary>
        TenantOnly = 0,

        /// <summary>
        /// Full card details including customer-specific information.
        /// </summary>
        CardDetails = 1
    }

    /// <summary>
    /// Public tenant information safe to return without authentication.
    /// Contains ONLY tenant-level branding/marketing data.
    /// </summary>
    public class TenantWalletPublicDto
    {
        public int TenantId { get; set; }
        public string TenantName { get; set; } = string.Empty;
        // In-app menyu üçün: slug (public menu endpoint-inin açarı) + flag + görünüş
        public string? Slug { get; set; }
        public bool IsHasMenu { get; set; }
        public string? DefaultMenuView { get; set; }
        public int? ProgramId { get; set; }
        public string? ProgramName { get; set; }
        public string? ProgramDescription { get; set; }
        public string? RewardDescription { get; set; }
        public string? GoogleLogoUrl { get; set; }
        public string? StripImageUrl { get; set; }
        public string? LocationUrl { get; set; }
        public string? ContactNumber { get; set; }
        public string? InstagramUrl { get; set; }
        public string? FacebookUrl { get; set; }
        public string? WhatsAppUrl { get; set; }
        public string? TelegramUrl { get; set; }
        public string? MenuUrl { get; set; }
        public string? LinkedInUrl { get; set; }
        public string? TiktokUrl { get; set; }
        public string? TripAdvisorUrl { get; set; }
        public string? WebsiteUrl { get; set; }
        public string? YoutubeUrl { get; set; }
        public string? TwitterUrl { get; set; }
        public string? YelpUrl { get; set; }
        public string? ThreadsUrl { get; set; }
        public string? PinterestUrl { get; set; }
        public string BackgroundColor { get; set; } = "#FFFFFF";
        public string ForegroundColor { get; set; } = "#000000";
        public decimal? CashbackPercent { get; set; }
        public decimal? CashbackPercentCash { get; set; }

        // App-specific images (.webp)
        public string? AppLogoUrl { get; set; }
        public string? AppStripUrl { get; set; }

        // Filiallar — çox-filiallı tenant üçün (Filiallar düyməsi + branch menyu seçici).
        public List<TenantWalletBranchDto> Branches { get; set; } = new();
    }

    /// <summary>
    /// Tenant filialı — kart detalında filiallar düyməsi + per-branch menyu üçün.
    /// </summary>
    public class TenantWalletBranchDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Slug { get; set; }
        public string? DefaultMenuView { get; set; }
        public string? AppDisplayName { get; set; }
        public bool IsMainBranch { get; set; }
        public bool HideInApp { get; set; }
    }

    /// <summary>
    /// Customer-specific card data. SENSITIVE - only return when authorized.
    /// These fields MUST NOT be populated in TenantOnly mode.
    /// </summary>
    public class WalletCardPrivateDto
    {
        public string CustomerFullName { get; set; } = string.Empty;
        public decimal CurrentPoints { get; set; }

        // ProgressBased card fields
        public string? progressText { get; set; }  // e.g., "5/10" (null for non-ProgressBased)
        public string? freeRewardText { get; set; }  // e.g., "1 Pulsuz içki" (null for non-ProgressBased)
    }

    /// <summary>
    /// Unified response DTO for wallet card details.
    /// When mode is TenantOnly, the Card property will be null.
    /// </summary>
    public class WalletCardDetailsResponseDto
    {
        /// <summary>
        /// Indicates whether this response contains card-specific data.
        /// </summary>
        public WalletCardDetailsMode Mode { get; set; }

        /// <summary>
        /// Public tenant information (always populated).
        /// </summary>
        public TenantWalletPublicDto Tenant { get; set; } = new();

        /// <summary>
        /// Customer-specific card details.
        /// NULL when Mode is TenantOnly.
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public WalletCardPrivateDto? Card { get; set; }
    }
}


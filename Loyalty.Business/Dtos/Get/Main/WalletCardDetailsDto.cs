using Loyalty.DataAccess.Enums;
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

        // Branding (from TenantBranding)
        public string? LogoUrl { get; set; }
        public string? BackgroundImageUrl { get; set; }
        public string? IconUrl { get; set; }
        public string PrimaryColor { get; set; } = "#000000";
        public string SecondaryColor { get; set; } = "#FFFFFF";
        public string BackgroundColor { get; set; } = "#FFFFFF";
        public string TextColor { get; set; } = "#000000";

        // Card Template Images (from CardTemplate)
        public string? GoogleLogoUrl { get; set; }
        public string? HeroImageUrl { get; set; }
        public string? StripImageUrl { get; set; }

        // Card Template Colors (from CardTemplate)
        public string CardBackgroundColor { get; set; } = "#FFFFFF";
        public string CardForegroundColor { get; set; } = "#000000";
        public string CardLabelColor { get; set; } = "#999999";

        // General info
        public string? Description { get; set; }
        public string? Website { get; set; }
        public string? LocationUrl { get; set; }
        public string? ContactEmail { get; set; }
        public string? ContactPhone { get; set; }

        // Category
        public int? CategoryId { get; set; }
        public string? CategoryAz { get; set; }
        public string? CategoryEn { get; set; }
        public string? CategoryRu { get; set; }

        // Program info (public, non-customer specific)
        public string? ProgramName { get; set; }
        public int? ProgramId { get; set; }
        public string? ProgramDescription { get; set; }
        public string? RewardDescription { get; set; }

        // Card template design (nested object with all design info)
        public CardDesignDto? Design { get; set; }
    }

    /// <summary>
    /// Customer-specific card data. SENSITIVE - only return when authorized.
    /// These fields MUST NOT be populated in TenantOnly mode.
    /// </summary>
    public class WalletCardPrivateDto
    {
        public int CardId { get; set; }
        public int CustomerId { get; set; }
        public int LoyaltyProgramId { get; set; }

        // Customer info
        public string CustomerFullName { get; set; } = string.Empty;
        public string? CustomerPhone { get; set; }

        // Card details (SENSITIVE)
        public string CardNumber { get; set; } = string.Empty;
        public string? BarcodeValue { get; set; }
        public BarcodeType BarcodeType { get; set; }

        // Points/Balance (SENSITIVE)
        public int CurrentPoints { get; set; }
        public int LifetimePoints { get; set; }
        public int CurrentSteps { get; set; }
        public int CompletedCycles { get; set; }

        // ProgressBased card fields
        public string? progressText { get; set; }  // e.g., "5/10" (null for non-ProgressBased)
        public string? freeRewardText { get; set; }  // e.g., "1 Pulsuz içki" (null for non-ProgressBased)

        // Tier/Cashback
        public string? TierName { get; set; }
        public decimal? CashbackPercent { get; set; }
        public decimal? CashbackPercentCash { get; set; }

        // Status
        public CardStatus CardStatus { get; set; }
        public DateTime? ActivatedAt { get; set; }
        public DateTime? ExpiresAt { get; set; }

        // Wallet platform info
        public bool IsAndroid { get; set; }
        public bool IsIOS { get; set; }
        public string? ApplePassUrl { get; set; }
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


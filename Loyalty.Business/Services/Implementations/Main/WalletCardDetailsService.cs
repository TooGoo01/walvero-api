using Loyalty.Business.Dtos.Get.Main;
using Loyalty.Business.Helpers;
using Loyalty.Business.Results;
using Loyalty.Business.Services.Abstractions.Main;
using Loyalty.DataAccess.Entities.Main;
using Loyalty.DataAccess.Enums;
using Loyalty.DataAccess.Repositories.Abstractions.Main;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;

namespace Loyalty.Business.Services.Implementations.Main
{
    /// <summary>
    /// Service for retrieving wallet card details with proper authorization.
    /// </summary>
    public class WalletCardDetailsService : IWalletCardDetailsService
    {
        private readonly ITenantProvider _tenantProvider;
        private readonly ITenantRepository _tenantRepository;
        private readonly ILoyaltyCardRepository _cardRepository;
        private readonly IConfiguration _configuration;
        private readonly ILogger<WalletCardDetailsService> _logger;

        public WalletCardDetailsService(
            ITenantProvider tenantProvider,
            ITenantRepository tenantRepository,
            ILoyaltyCardRepository cardRepository,
            IConfiguration configuration,
            ILogger<WalletCardDetailsService> logger)
        {
            _tenantProvider = tenantProvider;
            _tenantRepository = tenantRepository;
            _cardRepository = cardRepository;
            _configuration = configuration;
            _logger = logger;
        }

        /// <inheritdoc />
        public async Task<ServiceResult<WalletCardDetailsResponseDto>> GetTenantWalletInfoAsync(
            int tenantId,
            CancellationToken ct = default)
        {
            var baseUrl = _configuration["App:BaseUrl"] ?? "https://walvero.com";

            // Log without sensitive data
            _logger.LogInformation("Fetching tenant wallet info for TenantId: {TenantId}", tenantId);

            var tenant = await _tenantRepository.GetTenantForWalletDetailsAsync(tenantId, ct);
            if (tenant == null)
            {
                _logger.LogWarning("Tenant not found: {TenantId}", tenantId);
                return ServiceResult<WalletCardDetailsResponseDto>.Fail("Tenant not found");
            }

            var response = new WalletCardDetailsResponseDto
            {
                Mode = WalletCardDetailsMode.TenantOnly,
                Tenant = BuildTenantPublicDto(tenant, baseUrl),
                Card = null // Explicitly null - no card-specific data
            };

            return ServiceResult<WalletCardDetailsResponseDto>.Ok(response);
        }

        /// <inheritdoc />
        public async Task<ServiceResult<WalletCardDetailsResponseDto>> GetCardDetailsAsync(
            int tenantId,
            int cardId,
            CancellationToken ct = default)
        {
            var baseUrl = _configuration["App:BaseUrl"] ?? "https://walvero.com";

            // Log without sensitive data (no card numbers, barcodes, etc.)
            _logger.LogInformation(
                "Fetching card details for TenantId: {TenantId}, CardId: {CardId}",
                tenantId, cardId);

            // 1. Authorization check
            var currentUserId = _tenantProvider.CurrentUserId;
            if (string.IsNullOrEmpty(currentUserId))
            {
                _logger.LogWarning("No authenticated user for card access: {CardId}", cardId);
                return ServiceResult<WalletCardDetailsResponseDto>.Fail("Authentication required");
            }

            if (!int.TryParse(currentUserId, out var applicationUserId))
            {
                _logger.LogWarning("Invalid user ID format: {UserId}", currentUserId);
                return ServiceResult<WalletCardDetailsResponseDto>.Fail("Authentication required");
            }

            // 2. Single query: card + full details + ownership check
            var card = await _cardRepository.GetCardWithOwnerCheckAsync(cardId, applicationUserId, ct);
            if (card == null)
            {
                _logger.LogWarning("Card not found or access denied: {CardId}", cardId);
                return ServiceResult<WalletCardDetailsResponseDto>.Fail("Card not found");
            }

            // 3. Validate tenant match
            if (card.TenantId != tenantId)
            {
                _logger.LogWarning(
                    "Tenant mismatch: Card {CardId} belongs to Tenant {ActualTenantId}, requested Tenant {RequestedTenantId}",
                    cardId, card.TenantId, tenantId);
                return ServiceResult<WalletCardDetailsResponseDto>.Fail("Card not found");
            }

            // 4. Build response with full card details
            // Use card's LoyaltyProgram directly since it's already loaded with CardTemplate
            var response = new WalletCardDetailsResponseDto
            {
                Mode = WalletCardDetailsMode.CardDetails,
                Tenant = BuildTenantPublicDto(card.Tenant, card.LoyaltyProgram, card, baseUrl),
                Card = BuildCardPrivateDto(card, baseUrl)
            };

            return ServiceResult<WalletCardDetailsResponseDto>.Ok(response);
        }

        /// <summary>
        /// Build public tenant DTO from entity.
        /// Contains ONLY public, non-sensitive information.
        /// </summary>
        private TenantWalletPublicDto BuildTenantPublicDto(Tenant tenant, string baseUrl)
        {
            var branding = tenant.Branding;
            var category = tenant.Category;
            var program = tenant.LoyaltyPrograms?.FirstOrDefault(lp => lp.IsActive);
            return BuildTenantPublicDto(tenant, program, null, baseUrl);
        }

        /// <summary>
        /// Build public tenant DTO from entity with explicit program.
        /// Used when program is already loaded (e.g., from card).
        /// </summary>
        private TenantWalletPublicDto BuildTenantPublicDto(Tenant tenant, LoyaltyProgram? program, LoyaltyCard? card, string baseUrl)
        {
            var cardTemplate = program?.CardTemplate;

            string? stripImageUrl = ToAbsoluteUrl(cardTemplate?.StripImageUrl, baseUrl);
            if (cardTemplate != null && cardTemplate.TemplateType == CardTemplateType.ProgressBased && card != null)
            {
                var snapshot = CardTemplateSnapshotFactory.From(
                    program ?? new LoyaltyProgram { Id = 0, TenantId = 0 },
                    cardTemplate,
                    null,
                    path => ToAbsoluteUrl(path, baseUrl));

                int totalSteps = Math.Max(1, snapshot.TotalSteps);
                int currentSteps = Math.Clamp(card.CurrentSteps, 0, totalSteps);
                var progressIconUrl = PickProgressIcon(snapshot.IconUrls, currentSteps);

                if (!string.IsNullOrWhiteSpace(progressIconUrl))
                    stripImageUrl = progressIconUrl;
            }

            decimal? cashbackPercent = null;
            decimal? cashbackPercentCash = null;
            if (card != null)
            {
                (_, cashbackPercent, cashbackPercentCash) = CalculateTier(card.CurrentPoints, cardTemplate);
            }

            var dto = new TenantWalletPublicDto
            {
                TenantId = tenant.Id,
                TenantName = tenant.CompanyName,

                // Program info
                ProgramName = program?.ProgramName,
                ProgramId = program?.Id,
                ProgramDescription = program?.Description,
                RewardDescription = cardTemplate?.RewardDescription,

                // Card Template Images (from CardTemplate)
                GoogleLogoUrl = ToAbsoluteUrl(cardTemplate?.GoogleLogoUrl, baseUrl),
                StripImageUrl = stripImageUrl,

                LocationUrl = tenant.LocationUrl,
                ContactNumber = tenant.ContactPhone,
                InstagramUrl = tenant.InstagramUrl,
                FacebookUrl = tenant.FacebookUrl,
                WhatsAppUrl = tenant.WhatsAppUrl,
                TelegramUrl = tenant.TeleqramUrl,
                MenuUrl = tenant.MenuUrl,
                BackgroundColor = cardTemplate?.BackgroundColor ?? tenant.Branding?.BackgroundColor ?? "#FFFFFF",
                ForegroundColor = cardTemplate?.ForegroundColor ?? tenant.Branding?.TextColor ?? "#000000",
                CashbackPercent = cashbackPercent,
                CashbackPercentCash = cashbackPercentCash,

                // App-specific images (.webp)
                AppLogoUrl = ToAbsoluteUrl(cardTemplate?.AppLogoUrl, baseUrl),
                AppStripUrl = ToAbsoluteUrl(cardTemplate?.AppStripUrl, baseUrl),
            };

            return dto;
        }

        /// <summary>
        /// Build card private DTO from entity.
        /// Contains SENSITIVE customer-specific information.
        /// </summary>
        private WalletCardPrivateDto BuildCardPrivateDto(LoyaltyCard card, string baseUrl)
        {
            var customer = card.Customer;
            var program = card.LoyaltyProgram;
            var cardTemplate = program?.CardTemplate;

            string? progressText = null;
            string? freeRewardText = null;

            // ProgressBased card specific logic
            if (cardTemplate != null && cardTemplate.TemplateType == CardTemplateType.ProgressBased)
            {
                int totalSteps = Math.Max(1, cardTemplate.TotalSteps);
                int currentSteps = Math.Clamp(card.CurrentSteps, 0, totalSteps);
                
                // Progress text: "5/10"
                progressText = $"{currentSteps}/{totalSteps}";

                // Free reward text: "1 Pulsuz içki"
                int freeRewardCount = card.CompletedCycles;
                string freeRewardLabel = cardTemplate.FreeRewardLabel ?? "Pulsuz içki";
                freeRewardText = $"{freeRewardCount} {freeRewardLabel}";

            }

            return new WalletCardPrivateDto
            {
                CustomerFullName = $"{customer?.FirstName} {customer?.LastName}".Trim(),
                CurrentPoints = card.CurrentPoints,

                // ProgressBased card fields
                progressText = progressText,
                freeRewardText = freeRewardText
            };
        }

        /// <summary>
        /// Pick progress icon from icon list based on current step index.
        /// Returns icon URL if valid, otherwise null.
        /// </summary>
        private static string? PickProgressIcon(List<string>? icons, int curIndex)
        {
            if (icons == null || icons.Count == 0) return null;
            
            // Clamp index to valid range (0 -> 0th icon, 1 -> 1st icon, etc.)
            int clampedIndex = Math.Clamp(curIndex, 0, icons.Count - 1);
            var iconUrl = icons[clampedIndex]?.Trim();
            
            // Return the icon URL if it's not empty
            return string.IsNullOrWhiteSpace(iconUrl) ? null : iconUrl;
        }

        /// <summary>
        /// Calculate tier name and cashback percentages based on points.
        /// </summary>
        private static (string tierName, decimal? percent, decimal? percentCash) CalculateTier(
            decimal points,
            CardTemplate? cardTemplate)
        {
            if (cardTemplate == null)
                return ("Bronze", null, null);

            int bronzeTh = cardTemplate.BronzeThreshold ?? 0;
            int? silverTh = cardTemplate.SilverThreshold;
            int? goldTh = cardTemplate.GoldThreshold;
            int? platTh = cardTemplate.PlatinumThreshold;

            // Ensure thresholds are in ascending order
            if (silverTh.HasValue && silverTh.Value < bronzeTh) silverTh = bronzeTh;
            if (goldTh.HasValue && silverTh.HasValue && goldTh.Value < silverTh.Value) goldTh = silverTh;
            if (platTh.HasValue && goldTh.HasValue && platTh.Value < goldTh.Value) platTh = goldTh;

            string name = "Bronze";
            int? percent = cardTemplate.BronzePercent;
            int? percentCash = cardTemplate.BronzePercentCash;

            if (platTh.HasValue && points >= platTh.Value)
            {
                name = "Platinum";
                percent = cardTemplate.PlatinumPercent;
                percentCash = cardTemplate.PlatinumPercentCash;
            }
            else if (goldTh.HasValue && points >= goldTh.Value)
            {
                name = "Gold";
                percent = cardTemplate.GoldPercent;
                percentCash = cardTemplate.GoldPercentCash;
            }
            else if (silverTh.HasValue && points >= silverTh.Value)
            {
                name = "Silver";
                percent = cardTemplate.SilverPercent;
                percentCash = cardTemplate.SilverPercentCash;
            }

            return (name, percent, percentCash);
        }

        /// <summary>
        /// Convert database path to absolute URL.
        /// Handles Azure paths correctly by ensuring proper URL construction.
        /// </summary>
        private static string? ToAbsoluteUrl(string? dbPath, string baseUrl)
        {
            if (string.IsNullOrWhiteSpace(dbPath)) 
                return null;
            
            // If already an absolute URL, return as-is
            if (dbPath.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || 
                dbPath.StartsWith("https://", StringComparison.OrdinalIgnoreCase)) 
                return dbPath;
            
            // Normalize baseUrl - remove trailing slash if present
            var normalizedBaseUrl = baseUrl.TrimEnd('/', '\\');
            
            // Normalize path - remove leading "~/" if present, then trim leading slashes/backslashes
            var normalizedPath = dbPath.TrimStart('~');
            normalizedPath = normalizedPath.TrimStart('/', '\\');
            
            // Ensure path starts with "/" for proper URL construction
            if (!string.IsNullOrWhiteSpace(normalizedPath))
            {
                normalizedPath = "/" + normalizedPath;
            }
            else
            {
                normalizedPath = "/";
            }
            
            return normalizedBaseUrl + normalizedPath;
        }
    }
}

using AutoMapper;
using Loyalty.Business.Dtos.Get.Main;
using Loyalty.Business.Dtos.Post.Main;
using Loyalty.Business.Helpers;
using Loyalty.Business.Results;
using Loyalty.Business.Services.Abstractions.Main;
using Loyalty.DataAccess.Entities.Main;
using Loyalty.DataAccess.Entities.Users;
using Loyalty.DataAccess.Enums;
using Loyalty.DataAccess.Repositories.Abstractions.Main;
using Loyalty.DataAccess.Repositories.Implementations.Main;
using Loyalty.DataAccess.UnitOfWorks;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Loyalty.Business.Services.Implementations.Main
{
    public class UserCardService : IUserCardService
    {
        private readonly ITenantProvider _tp;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILoyaltyCardRepository _cardRepo;
        private readonly ILoyaltyProgramRepository _programRepo;
        private readonly ITenantRepository _tenantRepo;
        private readonly ITenantBrandingRepository _tenantBrandingRepository;
        private readonly IConfiguration _config;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;
        public UserCardService(
            ITenantProvider tp,
            UserManager<ApplicationUser> userManager,
            ILoyaltyCardRepository cardRepo,
            ILoyaltyProgramRepository programRepo,
            ITenantRepository tenantRepo,
            ITenantBrandingRepository tenantBrandingRepository,
            IConfiguration config,
            IUnitOfWork unitOfWork,
            IMapper mapper)
        {
            _tp = tp;
            _userManager = userManager;
            _cardRepo = cardRepo;
            _programRepo = programRepo;
            _tenantRepo = tenantRepo;
            _tenantBrandingRepository = tenantBrandingRepository;
            _config = config;
            _unitOfWork = unitOfWork;
            _mapper = mapper;
        }
        private static string? EnsureHttpsOrNull(string? url)
        {
            if (string.IsNullOrWhiteSpace(url)) return null;
            if (Uri.TryCreate(url, UriKind.Absolute, out var u) &&
                u.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase))
                return url;
            return null;
        }

        private static string? FirstNonEmpty(params string?[] urls)
        {
            foreach (var u in urls)
                if (!string.IsNullOrWhiteSpace(u)) return u;
            return null;
        }
        public static (string tierName, int? percent, int? percentCash, string? iconUrl) TierResolver(
int points, GetCardTemplateDto tpl, Func<string?, string?>? toAbsHttps = null)
        {
            string? H(string? url) =>
                toAbsHttps is null ? EnsureHttpsOrNull(url) : EnsureHttpsOrNull(toAbsHttps(url));

            // Threshold-lar (Bronze üçün default = 0)
            int bronzeTh = tpl.BronzeThreshold ?? 0;
            int? silverTh = tpl.SilverThreshold;
            int? goldTh = tpl.GoldThreshold;
            int? platTh = tpl.PlatinumThreshold;

            // Ardıcıllıq pozularsa, artan qaydada düzəlt (opsional, amma təhlükəsizdir)
            if (silverTh.HasValue && silverTh.Value < bronzeTh) silverTh = bronzeTh;
            if (goldTh.HasValue && silverTh.HasValue && goldTh.Value < silverTh.Value) goldTh = silverTh;
            if (platTh.HasValue && goldTh.HasValue && platTh.Value < goldTh.Value) platTh = goldTh;

            // Tier seçimi (>= minimal reqem)
            string name = "Bronze";
            int? percent = tpl.BronzePercent;
            int? percentCash = tpl.BronzePercentCash;
            string? icon = FirstNonEmpty(H(tpl.BronzeIconUrl), H(tpl.LogoUrl));

            if (platTh.HasValue && points >= platTh.Value)
            {
                name = "Platinum";
                percent = tpl.PlatinumPercent;
                percentCash = tpl.PlatinumPercentCash;
                icon = FirstNonEmpty(H(tpl.PlatinumIconUrl), icon);
            }
            else if (goldTh.HasValue && points >= goldTh.Value)
            {
                name = "Gold";
                percent = tpl.GoldPercent;
                percentCash = tpl.GoldPercentCash;
                icon = FirstNonEmpty(H(tpl.GoldIconUrl), icon);
            }
            else if (silverTh.HasValue && points >= silverTh.Value)
            {
                name = "Silver";
                percent = tpl.SilverPercent;
                percentCash = tpl.SilverPercentCash;
                icon = FirstNonEmpty(H(tpl.SilverIconUrl), icon);
            }
            else
            {
                // points < SilverThreshold → Bronze qalır (hətta < BronzeThreshold olsa belə)
                name = "Bronze";
                percent = tpl.BronzePercent;
                percentCash = tpl.BronzePercentCash;
                icon = FirstNonEmpty(H(tpl.BronzeIconUrl), H(tpl.LogoUrl));
            }

            return (name, percent, percentCash, icon);
        }

        /// <summary>
        /// Maps LoyaltyCard entity with navigation properties to UserCardDto.
        /// This method encapsulates the complex mapping logic for better maintainability.
        /// </summary>
        private static UserCardDto MapToUserCardDto(
            LoyaltyCard card,
            Tenant tenant,
            LoyaltyProgram program,
            CardTemplateSnapshot snapshot,
            string tierName,
            int? percent,
            int? percentCash,
            GetCardTemplateDto? template = null)
        {
            string? progressText = null;
            string? freeRewardText = null;

            // ProgressBased card specific logic
            if (snapshot.TemplateType == CardTemplateType.ProgressBased)
            {
                int totalSteps = Math.Max(1, snapshot.TotalSteps);
                int currentSteps = Math.Clamp(card.CurrentSteps, 0, totalSteps);
                
                // Progress text: "5/10"
                progressText = $"{currentSteps}/{totalSteps}";

                // Free reward text: "1 Pulsuz içki"
                int freeRewardCount = card.CompletedCycles;
                string freeRewardLabel = template?.FreeRewardLabel ?? snapshot.FreeRewardLabel ?? "Pulsuz içki";
                freeRewardText = $"{freeRewardCount} {freeRewardLabel}";
            }

            // Build design DTO (HeroImageUrl and StripImageUrl are NOT overridden for ProgressBased in GetMyCards)
            var design = new CardDesignDto
            {
                ProgramName = program.ProgramName,
                BackgroundColor = snapshot.BackgroundColor,
                ForegroundColor = snapshot.ForegroundColor,
                LabelColor = snapshot.LabelColor,
                HeroImageUrl = snapshot.HeroImageUrl,
                LogoImageUrl = snapshot.GoogleLogoUrl,
                StripImageUrl = snapshot.StripImageUrl,
                TierName = tierName,
                ProgramDescription = snapshot.ProgramDescription,
                RewardDescription = snapshot.RewardDescription,
            };

            return new UserCardDto
            {
                TenantId = card.TenantId,
                TenantName = tenant.CompanyName,
                CustomerFullName = $"{card.Customer.FirstName} {card.Customer.LastName}".Trim(),
                LocationUrl = tenant.LocationUrl,
                CardId = card.Id,
                ProgramId = card.LoyaltyProgramId,
                ProgramName = program.ProgramName,
                CurrentPoints = card.CurrentPoints,
                categoryId = tenant.categoryId,
                categoryAz = tenant.Category?.AzName,
                categoryEn = tenant.Category?.EnName,
                categoryRu = tenant.Category?.RuName,
                CardNumber = card.CardNumber,
                BarcodeValue = card.BarcodeValue,
                CashbackPercent = percent,
                CashbackPercentCash = percentCash,
                Design = design,
                progressText = progressText,
                freeRewardText = freeRewardText
            };
        }

        /// <summary>
        /// Pick progress icon from icon list based on current step index.
        /// Returns icon URL if valid, otherwise null.
        /// Note: IconUrls in snapshot are already absolute URLs from CardTemplateSnapshotFactory.
        /// </summary>
        private static string? PickProgressIcon(List<string>? icons, int curIndex)
        {
            if (icons == null || icons.Count == 0) return null;
            
            // Clamp index to valid range (0 -> 0th icon, 1 -> 1st icon, etc.)
            int clampedIndex = Math.Clamp(curIndex, 0, icons.Count - 1);
            var iconUrl = icons[clampedIndex]?.Trim();
            
            // Return the icon URL if it's not empty (even if not HTTPS)
            return string.IsNullOrWhiteSpace(iconUrl) ? null : iconUrl;
        }

        public async Task<ServiceResult> GetMyCardsAsync(
     int currentPage,
     int pageSize,
     TenantSearchDto model,
     CancellationToken ct = default)
        {
            // 0) Page size limiti
            if (pageSize > 100)
                pageSize = 100;

            // 1) Cari user
            var userName = _tp.CurrentUserId;
            if (string.IsNullOrWhiteSpace(userName))
                return new ServiceResult(false, "User not resolved.");

            var appUser = await _userManager.FindByIdAsync(userName);
            if (appUser == null)
                return new ServiceResult(false, "User not found.");

            var applicationUserId = appUser.Id;

            // 2) OPTIMIZATION: Get count first, then paginated cards in a single efficient query
            var totalCount = await _cardRepo.GetActiveCardsCountForApplicationUserAsync(
                applicationUserId,
                model.Name,
                model.categoryId,
                ct);

            if (totalCount == 0)
            {
                var emptyResponse = new SearchResult<IEnumerable<UserCardDto>>
                {
                    Value = Enumerable.Empty<UserCardDto>(),
                    DataCount = 0,
                    CurrentPage = currentPage,
                    PageSize = pageSize
                };

                return new ServiceResult(true, emptyResponse);
            }

            // 2.1) OPTIMIZATION: Get paginated cards directly from database with all includes
            // Cards query already includes: Customer, LoyaltyProgram.CardTemplate, Tenant.Category, Tenant.Branding
            var pagedCards = await _cardRepo.GetLastActiveCardsForApplicationUserPaginatedAsync(
                applicationUserId,
                model.Name,
                model.categoryId,
                currentPage,
                pageSize,
                ct);

            // BaseUrl
            var baseUrl = _config["App:BaseUrl"] ?? "https://walvero.com";

            string ToAbs(string? dbPath)
            {
                if (string.IsNullOrWhiteSpace(dbPath)) return string.Empty;
                if (dbPath.StartsWith("http", StringComparison.OrdinalIgnoreCase)) return dbPath;
                var rel = "/" + dbPath.TrimStart('/', '\\');
                return baseUrl + rel;
            }

            // 3) Snapshot cache - use data already loaded via Include
            var snapshotCache = new Dictionary<(int tenantId, int programId), CardTemplateSnapshot>();

            var resultList = new List<UserCardDto>();

            foreach (var card in pagedCards)
            {
                // Use navigation properties directly - data is already loaded via Include
                var tenant = card.Tenant;
                var program = card.LoyaltyProgram;

                if (tenant == null || program == null || program.CardTemplate == null)
                    continue;

                var key = (card.TenantId, card.LoyaltyProgramId);
                if (!snapshotCache.TryGetValue(key, out var snapshot))
                {
                    snapshot = CardTemplateSnapshotFactory.From(
                        program,
                        program.CardTemplate,
                        null, // Branding not needed
                        ToAbs);

                    snapshotCache[key] = snapshot;
                }

                // OPTIMIZATION: Use AutoMapper for CardTemplate → GetCardTemplateDto mapping
                var cardTemplate = program.CardTemplate;
                var tpl = _mapper.Map<GetCardTemplateDto>(cardTemplate);

                // TierResolver
                var (tierName, percent, percentCash, tierIconUrl) =
                    TierResolver(card.CurrentPoints, tpl, ToAbs);

                // OPTIMIZATION: Use helper method for complex mapping
                var dto = MapToUserCardDto(card, tenant, program, snapshot, tierName, percent, percentCash, tpl);

                resultList.Add(dto);
            }

            // 5) SearchResult kimi cavab qaytar
            var response = new SearchResult<IEnumerable<UserCardDto>>
            {
                Value = resultList,
                DataCount = totalCount,
                CurrentPage = currentPage,
                PageSize = pageSize
            };

            return new ServiceResult(true, response);
        }

        public async Task<ServiceResult> Delete(int id)
        {
            var category = await _cardRepo.GetByIdAsync(id);
            if (category != null)
            {
                category.IsActive = false;
                _unitOfWork.Repository<LoyaltyCard>().Update(category);
                _unitOfWork.Commit();
                return new ServiceResult(true);
            }
            return new ServiceResult(false);
        }
    }
}

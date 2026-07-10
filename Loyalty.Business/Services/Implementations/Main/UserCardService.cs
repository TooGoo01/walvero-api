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
        public UserCardService(
            ITenantProvider tp,
            UserManager<ApplicationUser> userManager,
            ILoyaltyCardRepository cardRepo,
            ILoyaltyProgramRepository programRepo,
            ITenantRepository tenantRepo,
            ITenantBrandingRepository tenantBrandingRepository,
            IConfiguration config,
            IUnitOfWork unitOfWork)
        {
            _tp = tp;
            _userManager = userManager;
            _cardRepo = cardRepo;
            _programRepo = programRepo;
            _tenantRepo = tenantRepo;
            _tenantBrandingRepository = tenantBrandingRepository;
            _config = config;
            _unitOfWork = unitOfWork;
        }
        /// <summary>
        /// Maps LoyaltyCard entity with navigation properties to UserCardDto.
        /// This method encapsulates the complex mapping logic for better maintainability.
        /// </summary>
        private static UserCardDto MapToUserCardDto(
            LoyaltyCard card,
            Tenant tenant,
            LoyaltyProgram program,
            CardTemplateSnapshot snapshot)
        {
            // Ödəniş suspensiyası — display-only mask: balans 0, progress/mükafat gizli.
            // DB dəyişmir; flag söndürüləndə real dəyərlər avtomatik qayıdır.
            bool suspended = tenant.IsServiceSuspended;

            string? progressText = null;
            string? freeRewardText = null;

            // ProgressBased card specific logic
            if (!suspended && snapshot.TemplateType == CardTemplateType.ProgressBased)
            {
                int totalSteps = Math.Max(1, snapshot.TotalSteps);
                int currentSteps = Math.Clamp(card.CurrentSteps, 0, totalSteps);
                
                // Progress text: "5/10"
                progressText = $"{currentSteps}/{totalSteps}";

                // Free reward text: "1 Pulsuz içki"
                int freeRewardCount = card.CompletedCycles;
                string freeRewardLabel = snapshot.FreeRewardLabel ?? "Pulsuz içki";
                freeRewardText = $"{freeRewardCount} {freeRewardLabel}";
            }

            // Build design DTO (minimal fields used in wallet list)
            var design = new CardDesignDto
            {
                BackgroundColor = snapshot.BackgroundColor,
                ForegroundColor = snapshot.ForegroundColor,
                LabelColor = snapshot.LabelColor,
                LogoImageUrl = snapshot.GoogleLogoUrl,
                AppLogoUrl = !string.IsNullOrWhiteSpace(snapshot.AppLogoUrl) ? snapshot.AppLogoUrl : snapshot.GoogleLogoUrl,
                AppStripUrl = (snapshot.TemplateType == CardTemplateType.ProgressBased)
                    ? snapshot.StripImageUrl
                    : (!string.IsNullOrWhiteSpace(snapshot.AppStripUrl) ? snapshot.AppStripUrl : snapshot.StripImageUrl),
            };

            return new UserCardDto
            {
                TenantId = card.TenantId,
                TenantName = tenant.CompanyName,
                LocationUrl = tenant.LocationUrl,
                CardId = card.Id,
                ProgramId = card.LoyaltyProgramId,
                ProgramName = program.ProgramName,
                ProgramDescription = snapshot.ProgramDescription,
                RewardDescription = snapshot.RewardDescription,
                CurrentPoints = suspended ? 0 : card.CurrentPoints,
                categoryId = tenant.categoryId,
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

            // 2) OPTIMIZATION: Count + paginated data tək DB roundtrip ilə
            var (totalCount, pagedCards) = await _cardRepo.GetMyCardsPaginatedAsync(
                applicationUserId,
                model?.Name,
                model?.categoryId,
                model?.countryId,
                model?.cityId,
                currentPage,
                pageSize,
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

            // BaseUrl
            var baseUrl = _config["App:BaseUrl"] ?? "https://appapi.walvero.com";

            string ToAbs(string? dbPath)
            {
                if (string.IsNullOrWhiteSpace(dbPath)) return string.Empty;
                if (dbPath.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || 
                    dbPath.StartsWith("https://", StringComparison.OrdinalIgnoreCase)) return dbPath;
                
                var normalizedBaseUrl = baseUrl.TrimEnd('/', '\\');
                var normalizedPath = dbPath.TrimStart('~').TrimStart('/', '\\');
                normalizedPath = string.IsNullOrWhiteSpace(normalizedPath) ? "/" : "/" + normalizedPath;
                return normalizedBaseUrl + normalizedPath;
            }

            // 3) Snapshot cache - use data already loaded via Include
            var snapshotCache = new Dictionary<(int tenantId, int programId), CardTemplateSnapshot>();

            var resultList = new List<UserCardDto>();

            foreach (var card in pagedCards)
            {
                // Use navigation properties directly - data is already loaded via Include
                var tenant = card.Tenant;
                var program = card.LoyaltyProgram;

                if (tenant == null || program == null)
                    continue;

                var key = (card.TenantId, card.LoyaltyProgramId);
                if (!snapshotCache.TryGetValue(key, out var snapshot))
                {
                    snapshot = CardTemplateSnapshotFactory.From(
                        program,
                        program.CardTemplate ?? new CardTemplate(),
                        null,
                        ToAbs);

                    snapshotCache[key] = snapshot;
                }

                // OPTIMIZATION: Use helper method - snapshot already has all needed data
                var dto = MapToUserCardDto(card, tenant, program, snapshot);

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
                // Keep Status consistent with deactivated state (MyCards filters Status == Active)
                category.Status = CardStatus.Cancelled;
                category.EditDate = DateTime.UtcNow;
                _unitOfWork.Repository<LoyaltyCard>().Update(category);
                _unitOfWork.Commit();
                return new ServiceResult(true);
            }
            return new ServiceResult(false);
        }
    }
}

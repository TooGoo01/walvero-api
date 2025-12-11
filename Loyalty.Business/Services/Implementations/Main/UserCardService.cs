using AutoMapper;
using Loyalty.Business.Dtos.Get.Main;
using Loyalty.Business.Dtos.Post.Main;
using Loyalty.Business.Helpers;
using Loyalty.Business.Results;
using Loyalty.Business.Services.Abstractions.Main;
using Loyalty.DataAccess.Entities.Main;
using Loyalty.DataAccess.Entities.Users;
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

            // 2) Userin bütün tenantlardakı son aktiv kartları (filtrlərlə birlikdə)
            var cards = await _cardRepo.GetLastActiveCardsForApplicationUserAsync(
                applicationUserId,
                model.Name,
                model.categoryId,
                ct);

            var totalCount = cards.Count;

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

            // 2.1) Sıralama + paging (istəsən Id yerinə başqa kritere görə sıralaya bilərsən)
            var pagedCards = cards
                .OrderBy(c => c.Id)
                .Skip((currentPage - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            // 3) Tenant, Program, Branding – artıq yalnız pagedCards üzrə lazımdır
            var tenantIds = pagedCards.Select(c => c.TenantId).Distinct().ToList();
            var programIds = pagedCards.Select(c => c.LoyaltyProgramId).Distinct().ToList();

            var tenants = await _tenantRepo.GetByIdsAsync(tenantIds, ct);
            var programs = await _programRepo.GetByIdsAsync(programIds, ct);

            var tenantDict = tenants.ToDictionary(t => t.Id);
            var programDict = programs.ToDictionary(p => p.Id);

            var brandingDict = new Dictionary<int, TenantBranding?>();

            foreach (var tid in tenantIds)
            {
                var b = await _tenantBrandingRepository.GetByTenantIdAsync(tid);
                brandingDict[tid] = b;
            }

            // BaseUrl
            var baseUrl = _config["App:BaseUrl"] ?? "https://walvero.com";

            string ToAbs(string? dbPath)
            {
                if (string.IsNullOrWhiteSpace(dbPath)) return string.Empty;
                if (dbPath.StartsWith("http", StringComparison.OrdinalIgnoreCase)) return dbPath;
                var rel = "/" + dbPath.TrimStart('/', '\\');
                return baseUrl + rel;
            }

            // 4) Snapshot cache
            var snapshotCache = new Dictionary<(int tenantId, int programId), CardTemplateSnapshot>();

            var resultList = new List<UserCardDto>();

            foreach (var card in pagedCards)
            {
                if (!tenantDict.TryGetValue(card.TenantId, out var tenant))
                    continue;

                if (!programDict.TryGetValue(card.LoyaltyProgramId, out var program))
                    continue;

                brandingDict.TryGetValue(card.TenantId, out var branding);

                var key = (card.TenantId, card.LoyaltyProgramId);
                if (!snapshotCache.TryGetValue(key, out var snapshot))
                {
                    snapshot = CardTemplateSnapshotFactory.From(
                        program,
                        program.CardTemplate,
                        branding,
                        ToAbs);

                    snapshotCache[key] = snapshot;
                }

                // CardTemplate-dən TierResolver üçün DTO
                var tpl = new GetCardTemplateDto
                {
                    BronzeThreshold = program.CardTemplate.BronzeThreshold,
                    BronzePercent = program.CardTemplate.BronzePercent,
                    BronzePercentCash = program.CardTemplate.BronzePercentCash,
                    BronzeIconUrl = program.CardTemplate.BronzeIconUrl,
                    SilverThreshold = program.CardTemplate.SilverThreshold,
                    SilverPercent = program.CardTemplate.SilverPercent,
                    SilverPercentCash = program.CardTemplate.SilverPercentCash,
                    SilverIconUrl = program.CardTemplate.SilverIconUrl,
                    GoldThreshold = program.CardTemplate.GoldThreshold,
                    GoldPercent = program.CardTemplate.GoldPercent,
                    GoldPercentCash = program.CardTemplate.GoldPercentCash,
                    GoldIconUrl = program.CardTemplate.GoldIconUrl,
                    PlatinumThreshold = program.CardTemplate.PlatinumThreshold,
                    PlatinumPercent = program.CardTemplate.PlatinumPercent,
                    PlatinumPercentCash = program.CardTemplate.PlatinumPercentCash,
                    PlatinumIconUrl = program.CardTemplate.PlatinumIconUrl,
                    LogoUrl = program.CardTemplate.GoogleLogoUrl
                };

                // TierResolver
                var (tierName, percent, percentCash, tierIconUrl) =
                    TierResolver(card.CurrentPoints, tpl, ToAbs);

                var dto = new UserCardDto
                {
                    TenantId = card.TenantId,
                    TenantName = tenant.CompanyName,
                    CustomerFullName = card.Customer.FirstName + " " + card.Customer.LastName,

                    CardId = card.Id,
                    ProgramId = card.LoyaltyProgramId,
                    ProgramName = program.ProgramName,

                    CurrentPoints = card.CurrentPoints,
                    categoryId=tenant.categoryId,
                    categoryAz=tenant!.Category.AzName,
                    categoryEn=tenant!.Category.EnName,
                    categoryRu=tenant!.Category.RuName,
                    CardNumber = card.CardNumber,
                    BarcodeValue = card.BarcodeValue,

                    CashbackPercent = percent,
                    CashbackPercentCash = percentCash,

                    Design = new CardDesignDto
                    {
                        ProgramName = program.ProgramName,
                        BackgroundColor = snapshot.BackgroundColor,
                        ForegroundColor = snapshot.ForegroundColor,
                        LabelColor = snapshot.LabelColor,
                        HeroImageUrl = snapshot.HeroImageUrl,
                        LogoImageUrl = snapshot.GoogleLogoUrl,
                        StripImageUrl = snapshot.StripImageUrl,
                        TierName = tierName
                    }
                };

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

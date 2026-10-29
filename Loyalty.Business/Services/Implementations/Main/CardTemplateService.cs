using AutoMapper;
using Loyalty.Business.Dtos.Get.Main;
using Loyalty.Business.Dtos.Post.Main;
using Loyalty.Business.Results;
using Loyalty.Business.Services.Abstractions.Main;
using Loyalty.Business.Services.Abstractions.Storage;
using Loyalty.DataAccess.Entities.Main;
using Loyalty.DataAccess.Enums;
using Loyalty.DataAccess.Repositories.Abstractions.Main;
using Loyalty.DataAccess.UnitOfWorks;
using Loyalty.Business.Helpers;
using Loyalty.DataAccess.Context;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Webp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Loyalty.Business.Services.Implementations.Main
{
    public class CardTemplateService : ICardTemplateService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;
        private readonly ICardTemplateRepository _repo;
        private readonly ILoyaltyProgramRepository _loyaltyProgramRepository;
        private readonly ICardTemplateIconService _cardTemplateIconService;
        private readonly IStorage _storage;
        private readonly IGoogleWalletServiceFactory? _googleFactory;
        private readonly ITenantBrandingRepository? _brandingRepo;
        private readonly LoyaltyWalletDbContext? _db;
        private readonly ILogger<CardTemplateService>? _logger;

        public CardTemplateService(IStorage storage, ICardTemplateRepository repo, IMapper mapper, IUnitOfWork unitOfWork, ICardTemplateIconService cardTemplateIconService, ILoyaltyProgramRepository loyaltyProgramRepository,
            IGoogleWalletServiceFactory? googleFactory = null, ITenantBrandingRepository? brandingRepo = null, LoyaltyWalletDbContext? db = null, ILogger<CardTemplateService>? logger = null)
        {
            _storage = storage;
            _repo = repo;
            _mapper = mapper;
            _unitOfWork = unitOfWork;
            _cardTemplateIconService = cardTemplateIconService;
            _loyaltyProgramRepository = loyaltyProgramRepository;
            _googleFactory = googleFactory;
            _brandingRepo = brandingRepo;
            _db = db;
            _logger = logger;
        }

        private static readonly System.Text.RegularExpressions.Regex HexColor = new("^#[0-9A-Fa-f]{6}$", System.Text.RegularExpressions.RegexOptions.Compiled);

        public Task<ServiceResult> Add(AddCardTemplateDto Dto)
        {
            throw new NotImplementedException();
        }

        public async Task<ServiceResult> AddOrUpdateAsync(AddCardTemplateDto dto, CancellationToken ct = default)
        {
            if (dto is null) return new ServiceResult(false, "Model is null.");
            if (dto.LoyaltyProgramId <= 0) return new ServiceResult(false, "LoyaltyProgramId is required.");
            if (string.IsNullOrWhiteSpace(dto.TemplateName)) return new ServiceResult(false, "TemplateName is required.");

            // Tier sahələri yalnız TierBased + PointsBased üçün relevantdir — ProgressBased halında təmizlə.
            // PointsBased single-tier üçün Bronze* field-ləri reuse edir → null etmə.
            if (dto.TemplateType == CardTemplateType.ProgressBased)
            {
                dto.BronzeThreshold = dto.SilverThreshold = dto.GoldThreshold = dto.PlatinumThreshold = null;
                dto.BronzePercent = dto.SilverPercent = dto.GoldPercent = dto.PlatinumPercent = null;
                dto.BronzePercentCash = dto.SilverPercentCash = dto.GoldPercentCash = dto.PlatinumPercentCash = null;
            }
            else if (dto.TemplateType == CardTemplateType.PointsBased)
            {
                // PointsBased yalnız Bronze* istifadə edir — Silver/Gold/Platinum təmizlə
                dto.SilverThreshold = dto.GoldThreshold = dto.PlatinumThreshold = null;
                dto.SilverPercent = dto.GoldPercent = dto.PlatinumPercent = null;
                dto.SilverPercentCash = dto.GoldPercentCash = dto.PlatinumPercentCash = null;
            }

            // 1) Mövcud template
            var entity = await _repo.GetByLoyaltyProgramIdAsync(dto.LoyaltyProgramId, ct)
                        ?? new CardTemplate { LoyaltyProgramId = dto.LoyaltyProgramId };

            // 2) Upload container
            var tenantId = ResolveTenantIdFromProgram(dto.LoyaltyProgramId); // öz implementasiyanız
            var container = $"tenant-{tenantId}-program-{dto.LoyaltyProgramId}-template";

            // 3) Şəkillər (file varsa yüklə, yoxdursa url saxla)
            entity.LogoUrl = await UpOrNullLink(container, "logo", dto.LogoFile, dto.LogoUrl, entity.LogoUrl, ct);
            entity.GoogleLogoUrl = await UpOrNullLink(container, "google-logo", dto.GoogleLogoFile, dto.GoogleLogoUrl, entity.GoogleLogoUrl, ct);
            entity.BackgroundImageUrl = await UpOrNullLink(container, "bg", dto.BackgroundImageFile, dto.BackgroundImageUrl, entity.BackgroundImageUrl, ct);
            entity.StripImageUrl = await UpOrLink(container, "strip", dto.StripImageFile, dto.StripImageUrl, entity.StripImageUrl, ct);
            entity.HeroImageUrl = await UpOrLink(container, "hero", dto.HeroImageFile, dto.HeroImageUrl, entity.HeroImageUrl, ct);

            // App-specific images — avtomatik .webp-yə çevrilir
            entity.AppLogoUrl = await UpAsWebpOrNullLink(container, "app-logo", dto.AppLogoFile, dto.AppLogoUrl, entity.AppLogoUrl, ct);
            entity.AppStripUrl = await UpAsWebpOrNullLink(container, "app-strip", dto.AppStripFile, dto.AppStripUrl, entity.AppStripUrl, ct);

            // Points-based icon faylları (bronze/silver/gold/platinum)
            entity.BronzeIconUrl = await UpOrNullLink(container, "bronze", dto.BronzeIconFile, dto.BronzeIconUrl, entity.BronzeIconUrl, ct);
            entity.SilverIconUrl = await UpOrNullLink(container, "silver", dto.SilverIconFile, dto.SilverIconUrl, entity.SilverIconUrl, ct);
            entity.GoldIconUrl = await UpOrNullLink(container, "gold", dto.GoldIconFile, dto.GoldIconUrl, entity.GoldIconUrl, ct);
            entity.PlatinumIconUrl = await UpOrNullLink(container, "platinum", dto.PlatinumIconFile, dto.PlatinumIconUrl, entity.PlatinumIconUrl, ct);

            // 4) Digər sahələr
            entity.TemplateName = dto.TemplateName.Trim();
            entity.TemplateType = dto.TemplateType;
            entity.TotalSteps = Math.Max(1, dto.TotalSteps);
            entity.RewardAsPoints = dto.TemplateType == CardTemplateType.ProgressBased && dto.RewardAsPoints;
            entity.StepLabel = dto.StepLabel?.Trim() ?? "Step";
            entity.RewardDescription = dto.RewardDescription?.Trim() ?? "";
            entity.FreeRewardLabel = dto.FreeRewardLabel?.Trim();  // nullable, default "Pulsuz içki" istifadəçi tərəfində

            entity.BronzeThreshold = dto.BronzeThreshold;
            entity.BronzePercent = dto.BronzePercent;
            entity.BronzePercentCash = dto.BronzePercentCash;
            entity.SilverThreshold = dto.SilverThreshold;
            entity.SilverPercent = dto.SilverPercent;
            entity.SilverPercentCash = dto.SilverPercentCash;
            entity.GoldThreshold = dto.GoldThreshold;
            entity.GoldPercent = dto.GoldPercent;
            entity.GoldPercentCash = dto.GoldPercentCash;
            entity.PlatinumThreshold = dto.PlatinumThreshold;
            entity.PlatinumPercent = dto.PlatinumPercent;
            entity.PlatinumPercentCash = dto.PlatinumPercentCash;
            entity.LoyaltyProgramId = dto.LoyaltyProgramId;
            entity.BackgroundColor = dto.BackgroundColor;
            entity.ForegroundColor = dto.ForegroundColor;
            entity.LabelColor = dto.LabelColor;
            entity.ProgressBarColor = dto.ProgressBarColor;
            entity.IconLayout = dto.IconLayout;
            entity.BronzeColor = dto.BronzeColor;
            entity.SilverColor = dto.SilverColor;
            entity.GoldColor = dto.GoldColor;
            entity.PlatinumColor = dto.PlatinumColor;
            entity.Currency = string.IsNullOrWhiteSpace(dto.Currency) ? "AZN" : dto.Currency.Trim();
            entity.PayMentType = dto.PayMentType;

            // 5) PROGRESS ikonları: AddRangeAsync köhnələri silir, yenilərini blob-a yükləyir

            // 6) Yadda saxla
            if (entity.Id == 0)
                await _unitOfWork.Repository<CardTemplate>().AddAsync(entity);
            else
                _unitOfWork.Repository<CardTemplate>().Update(entity);
            _unitOfWork.Commit();

            // LoyaltyProgram.CardTemplateId-ni yenilə (tracked query ilə)
            var program = await _loyaltyProgramRepository.GetByIdTrackedAsync(dto.LoyaltyProgramId, ct);
            if (program != null && program.CardTemplateId != entity.Id)
            {
                program.CardTemplateId = entity.Id;
                _unitOfWork.Repository<LoyaltyProgram>().Update(program);
                _unitOfWork.Commit();
            }
            // Yalnız real icon məlumatı (file və ya url) gəlibsə AddRangeAsync çağır.
            // User yeni icon əlavə etmirsə (boş list, null, və ya yalnız boş placeholder item-lər)
            // → mövcud Icons cədvəlinə toxunmuruq.
            var hasRealIcons = dto.Icons != null && dto.Icons.Any(i =>
                (i.IconFile != null && i.IconFile.Length > 0) ||
                !string.IsNullOrWhiteSpace(i.IconUrl));

            if (hasRealIcons)
                await _cardTemplateIconService.AddRangeAsync(dto, entity.Id, ct);
            // local obyekt üçün
            // 7) Cavab
            var result = MapToGetDto(entity);
            return new ServiceResult(true, result);
        }
        public async Task<ServiceResult> GetMyDesignAsync(int tenantId, int loyaltyProgramId, CancellationToken ct = default)
        {
            var program = await _loyaltyProgramRepository.GetByIdNameAsync(loyaltyProgramId, ct);
            if (program is null || program.TenantId != tenantId)
                return new ServiceResult(false, "Proqram tapılmadı.");

            var entity = await _repo.GetByLoyaltyProgramIdAsync(loyaltyProgramId, ct);
            if (entity is null)
                return new ServiceResult(true, new GetCardTemplateDto { LoyaltyProgramId = loyaltyProgramId });

            return new ServiceResult(true, MapToGetDto(entity));
        }

        public async Task<ServiceResult> UpdateMyDesignAsync(int tenantId, UpdateMyCardDesignDto dto, CancellationToken ct = default)
        {
            if (dto == null || dto.LoyaltyProgramId <= 0)
                return new ServiceResult(false, "Proqram tələb olunur.");

            // Tenant sahibliyi — başqa tenant proqramını redaktə edə bilməz
            var program = await _loyaltyProgramRepository.GetByIdNameAsync(dto.LoyaltyProgramId, ct);
            if (program is null || program.TenantId != tenantId)
                return new ServiceResult(false, "Proqram tapılmadı.");

            var entity = await _repo.GetByLoyaltyProgramIdAsync(dto.LoyaltyProgramId, ct)
                        ?? new CardTemplate { LoyaltyProgramId = dto.LoyaltyProgramId };

            var container = $"tenant-{tenantId}-program-{dto.LoyaltyProgramId}-template";

            // Rəng validasiyası — Apple/Google #RRGGBB tələb edir
            foreach (var c in new[] { dto.BackgroundColor, dto.ForegroundColor, dto.LabelColor, dto.ProgressBarColor, dto.BronzeColor, dto.SilverColor, dto.GoldColor, dto.PlatinumColor })
                if (!string.IsNullOrWhiteSpace(c) && !HexColor.IsMatch(c.Trim()))
                    return new ServiceResult(false, $"Rəng #RRGGBB formatında olmalıdır: '{c}'.");
            var site = dto.WebsiteUrl?.Trim();
            if (!string.IsNullOrWhiteSpace(site) && site != CardDesignSettings.Hidden
                && !(Uri.TryCreate(site, UriKind.Absolute, out var siteUri) && (siteUri.Scheme == Uri.UriSchemeHttp || siteUri.Scheme == Uri.UriSchemeHttps)))
                return new ServiceResult(false, "Sayt linki http(s):// ilə başlamalıdır.");

            // Şəkli silmə flag-ları (strip/hero UpOrLink boş gələndə köhnəni saxlayır)
            if (dto.ClearStripImage) { dto.StripImageFile = null; dto.StripImageUrl = null; entity.StripImageUrl = null; }
            if (dto.ClearHeroImage) { dto.HeroImageFile = null; dto.HeroImageUrl = null; entity.HeroImageUrl = null; }
            if (dto.ClearBackgroundImage) { dto.BackgroundImageFile = null; dto.BackgroundImageUrl = null; entity.BackgroundImageUrl = null; }

            // YALNIZ şəkillər (mövcud upload helper-ləri reuse)
            entity.LogoUrl = await UpOrNullLink(container, "logo", dto.LogoFile, dto.LogoUrl, entity.LogoUrl, ct);
            entity.GoogleLogoUrl = await UpOrNullLink(container, "google-logo", dto.GoogleLogoFile, dto.GoogleLogoUrl, entity.GoogleLogoUrl, ct);
            entity.BackgroundImageUrl = await UpOrNullLink(container, "bg", dto.BackgroundImageFile, dto.BackgroundImageUrl, entity.BackgroundImageUrl, ct);
            entity.StripImageUrl = await UpOrLink(container, "strip", dto.StripImageFile, dto.StripImageUrl, entity.StripImageUrl, ct);
            entity.HeroImageUrl = await UpOrLink(container, "hero", dto.HeroImageFile, dto.HeroImageUrl, entity.HeroImageUrl, ct);
            entity.AppLogoUrl = await UpAsWebpOrNullLink(container, "app-logo", dto.AppLogoFile, dto.AppLogoUrl, entity.AppLogoUrl, ct);
            entity.AppStripUrl = await UpAsWebpOrNullLink(container, "app-strip", dto.AppStripFile, dto.AppStripUrl, entity.AppStripUrl, ct);
            entity.BronzeIconUrl = await UpOrNullLink(container, "bronze", dto.BronzeIconFile, dto.BronzeIconUrl, entity.BronzeIconUrl, ct);
            entity.SilverIconUrl = await UpOrNullLink(container, "silver", dto.SilverIconFile, dto.SilverIconUrl, entity.SilverIconUrl, ct);
            entity.GoldIconUrl = await UpOrNullLink(container, "gold", dto.GoldIconFile, dto.GoldIconUrl, entity.GoldIconUrl, ct);
            entity.PlatinumIconUrl = await UpOrNullLink(container, "platinum", dto.PlatinumIconFile, dto.PlatinumIconUrl, entity.PlatinumIconUrl, ct);

            // Rənglər — yalnız gələn dəyər boş deyilsə (mövcudu qoru)
            if (!string.IsNullOrWhiteSpace(dto.BackgroundColor)) entity.BackgroundColor = dto.BackgroundColor.Trim();
            if (!string.IsNullOrWhiteSpace(dto.ForegroundColor)) entity.ForegroundColor = dto.ForegroundColor.Trim();
            if (!string.IsNullOrWhiteSpace(dto.LabelColor)) entity.LabelColor = dto.LabelColor.Trim();
            if (!string.IsNullOrWhiteSpace(dto.ProgressBarColor)) entity.ProgressBarColor = dto.ProgressBarColor.Trim();
            if (!string.IsNullOrWhiteSpace(dto.BronzeColor)) entity.BronzeColor = dto.BronzeColor.Trim();
            if (!string.IsNullOrWhiteSpace(dto.SilverColor)) entity.SilverColor = dto.SilverColor.Trim();
            if (!string.IsNullOrWhiteSpace(dto.GoldColor)) entity.GoldColor = dto.GoldColor.Trim();
            if (!string.IsNullOrWhiteSpace(dto.PlatinumColor)) entity.PlatinumColor = dto.PlatinumColor.Trim();

            // Kart dizayneri mətnləri — mövcud JSON-la merge (null = toxunma, "" = default-a qayıt)
            var design = CardDesignSettings.Parse(entity.DesignJson);
            design.Apply(dto.Title, dto.LogoText, dto.Description, dto.IssuerName,
                dto.BalanceLabel, dto.CustomerLabel, dto.CashbackLabel,
                dto.AboutTitle, dto.AboutText, dto.WebsiteUrl, dto.WebsiteLabel);
            if (dto.BalanceFormat is < 0 or > 2)
                return new ServiceResult(false, "Balans formatı yanlışdır.");
            design.ApplyBalance(dto.BalanceUnit, dto.BalanceFormat, dto.BalanceNotify, dto.BalanceChangeText);

            // Faza 2: sahə yuvaları / barkod / Google banner (LayoutJson)
            var layoutWarnings = new List<string>();
            if (!string.IsNullOrWhiteSpace(dto.LayoutJson))
            {
                var layout = CardLayoutUpdateDto.Parse(dto.LayoutJson, out var layoutError);
                if (layout == null) return new ServiceResult(false, layoutError ?? "LayoutJson yanlışdır.");
                design.ApplyLayout(layout);
                CardFieldCatalog.Effective(design.Fields, entity.EffectiveType, out layoutWarnings);
                CardFieldCatalog.GoogleRows(design.EffectiveFields(entity.EffectiveType), design.EffectiveGoogleBanner(entity.EffectiveType), out var rowWarnings);
                layoutWarnings.AddRange(rowWarnings);
            }
            entity.DesignJson = design.ToJson();

            // DİQQƏT: steps/threshold/percent/reward/TemplateName TOXUNULMUR.

            if (entity.Id == 0)
            {
                // Yeni template — minimal məcburi sahələr default qalır (loyalty konfiqi super admin-dədir)
                await _unitOfWork.Repository<CardTemplate>().AddAsync(entity);
            }
            else
            {
                _unitOfWork.Repository<CardTemplate>().Update(entity);
            }
            _unitOfWork.Commit();

            var tracked = await _loyaltyProgramRepository.GetByIdTrackedAsync(dto.LoyaltyProgramId, ct);
            if (tracked != null && tracked.CardTemplateId != entity.Id)
            {
                tracked.CardTemplateId = entity.Id;
                _unitOfWork.Repository<LoyaltyProgram>().Update(tracked);
                _unitOfWork.Commit();
            }

            // Google Wallet class (başlıq/issuer/rəng/logo) — obyekt PATCH-ləri class-a toxunmur, dizayn yalnız belə çatır. Best-effort.
            var googleMsg = await TryUpdateGoogleClassAsync(tenantId, program, entity, ct);
            var msg = string.Join(" ", new[] { googleMsg }.Concat(layoutWarnings).Where(m => !string.IsNullOrWhiteSpace(m)));

            return new ServiceResult(true, MapToGetDto(entity), msg);
        }

        /// <summary>Google class dizaynını yenilə; nəticə mesajı (UI-da göstərilir), xəta atmır.</summary>
        private async Task<string?> TryUpdateGoogleClassAsync(int tenantId, LoyaltyProgram program, CardTemplate entity, CancellationToken ct)
        {
            if (_googleFactory == null) return null;
            // Faza 0 (risk): real Google class PATCH-i yalnız flag açılanda (reviewStatus davranışı test tenant-da sınanana qədər bağlı)
            if (!CardDesignFeatureFlags.GoogleClassUpdateEnabled)
                return "Google Wallet class yeniləməsi hələ aktiv deyil (dəyişiklik növbəti aktivləşmədə tətbiq olunacaq).";
            try
            {
                var branding = _brandingRepo != null ? await _brandingRepo.GetByTenantIdAsync(tenantId, ct) : null;
                var companyName = _db != null
                    ? await _db.Tenants.AsNoTracking().Where(t => t.Id == tenantId).Select(t => t.CompanyName).FirstOrDefaultAsync(ct)
                    : null;
                static string ToAbs(string? p) => string.IsNullOrWhiteSpace(p) ? string.Empty
                    : (p.StartsWith("http", StringComparison.OrdinalIgnoreCase) ? p : "https://appapi.walvero.com/" + p.TrimStart('/', '\\'));
                var snapshot = CardTemplateSnapshotFactory.From(program, entity, branding, ToAbs);
                snapshot.TenantCompanyName = companyName;

                var gw = await _googleFactory.CreateForTenantAsync(tenantId, program.Id, ct);
                var res = await gw.UpdateClassDesignAsync(snapshot, ct);
                _logger?.LogInformation("Card design save: Google class update. TenantId={TenantId}, ProgramId={ProgramId}, Success={Success}, Message={Message}",
                    tenantId, program.Id, res.Success, res.Message);
                return res.Success ? "Google Wallet class yeniləndi." : $"Google Wallet class yenilənmədi: {res.Message}";
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Card design save: Google class update failed. TenantId={TenantId}, ProgramId={ProgramId}", tenantId, program.Id);
                return "Google Wallet class yenilənmədi (inteqrasiya yoxdur və ya xəta).";
            }
        }

        private async Task<string?> UpOrLink(
       string container, string prefix, IFormFile? file, string? url, string? oldValue, CancellationToken ct)
        {
            if (file is not null && file.Length > 0)
            {
                // köhnəni sil (yalnız öz storage yolundadırsa)
                if (!string.IsNullOrWhiteSpace(oldValue))
                {
                    var oldName = GetFileNameSafe(oldValue);
                    if (!string.IsNullOrEmpty(oldName))
                        await _storage.DeleteAsync(container, oldName);
                }

                var uploaded = await _storage.UploadFileAsync(container, new[] { file });
                var (_, relPath) = uploaded.First();
                return EnsureRelative(relPath);
            }
            else if (!string.IsNullOrWhiteSpace(url))
            {
                return url.Trim();
            }
            return oldValue; // heç nə gəlməyibsə köhnə qalsın
        }
        private async Task<string?> UpOrNullLink(
    string container, string prefix, IFormFile? file, string? url, string? oldValue, CancellationToken ct)
        {
            if (file is not null && file.Length > 0)
            {
                // köhnəni sil (yalnız öz storage yolundadırsa)
                if (!string.IsNullOrWhiteSpace(oldValue))
                {
                    var oldName = GetFileNameSafe(oldValue);
                    if (!string.IsNullOrEmpty(oldName))
                        await _storage.DeleteAsync(container, oldName);
                }

                var uploaded = await _storage.UploadFileAsync(container, new[] { file });
                var (_, relPath) = uploaded.First();
                return EnsureRelative(relPath);
            }
            else if (!string.IsNullOrWhiteSpace(url))
            {
                return url.Trim();
            }
            return null; // heç nə gəlməyibsə köhnə qalsın
        }
        /// <summary>
        /// Yüklənən şəkili avtomatik .webp formatına çevirib saxlayır.
        /// </summary>
        private async Task<string?> UpAsWebpOrNullLink(
            string container, string prefix, IFormFile? file, string? url, string? oldValue, CancellationToken ct)
        {
            if (file is not null && file.Length > 0)
            {
                if (!string.IsNullOrWhiteSpace(oldValue))
                {
                    var oldName = GetFileNameSafe(oldValue);
                    if (!string.IsNullOrEmpty(oldName))
                        await _storage.DeleteAsync(container, oldName);
                }

                // Şəkili .webp-yə çevir
                using var inputStream = file.OpenReadStream();
                using var image = await Image.LoadAsync(inputStream, ct);
                using var webpStream = new MemoryStream();
                await image.SaveAsWebpAsync(webpStream, new WebpEncoder { Quality = 80 }, ct);
                webpStream.Position = 0;

                var webpFileName = $"{prefix}-{Random.Shared.Next(1000, 9999)}.webp";
                var webpFile = new FormFile(webpStream, 0, webpStream.Length, file.Name, webpFileName)
                {
                    Headers = file.Headers,
                    ContentType = "image/webp"
                };

                var uploaded = await _storage.UploadFileAsync(container, new[] { webpFile });
                var (_, relPath) = uploaded.First();
                return EnsureRelative(relPath);
            }
            else if (!string.IsNullOrWhiteSpace(url))
            {
                return url.Trim();
            }
            return null;
        }

        private static string EnsureRelative(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return path;
            var p = path.Replace("\\", "/");
            var idx = p.IndexOf("/resource/", StringComparison.OrdinalIgnoreCase);
            if (idx >= 0) return p[idx..];
            idx = p.IndexOf("/uploads/", StringComparison.OrdinalIgnoreCase);
            return idx >= 0 ? p[idx..] : p.TrimStart('/');
        }

        private static string? GetFileNameSafe(string? urlOrPath)
        {
            if (string.IsNullOrWhiteSpace(urlOrPath)) return null;
            var u = urlOrPath.Replace("\\", "/");
            var last = u.Split('/').LastOrDefault();
            return string.IsNullOrWhiteSpace(last) ? null : last;
        }

        // LoyaltyProgram → TenantId həlli səndə necədirsə onu istifadə et
        private int ResolveTenantIdFromProgram(int loyaltyProgramId)
        {
            // Sadə placeholder: əgər konteyner yolunda tenant lazım deyilsə, 0 qaytara bilərsən.
            return 1;
        }

        private static GetCardTemplateDto MapToGetDto(CardTemplate e)
        {
            return new GetCardTemplateDto
            {
                Id = e.Id,
                LoyaltyProgramId = e.LoyaltyProgramId,
                TemplateName = e.TemplateName,
                TemplateType = e.TemplateType,
                Icons = e.Icons?.OrderBy(x => x.Order).Select(x => new IconDto
                {
                    Id = x.Id,
                    Name = x.Name,
                    IconUrl = x.IconUrl ?? x.Path,
                    Order = x.Order
                }).ToList() ?? new List<IconDto>(),
                TotalSteps = e.TotalSteps,
                RewardAsPoints = e.RewardAsPoints,
                StepLabel = e.StepLabel,
                RewardDescription = e.RewardDescription,
                BronzeIconUrl = e.BronzeIconUrl,
                BronzeThreshold = e.BronzeThreshold,
                SilverIconUrl = e.SilverIconUrl,
                SilverThreshold = e.SilverThreshold,
                SilverPercent = e.SilverPercent,
                BronzePercent = e.BronzePercent,
                GoldPercent = e.GoldPercent,
                PlatinumPercent = e.PlatinumPercent,
                BronzePercentCash = e.BronzePercentCash,
                SilverPercentCash = e.SilverPercentCash,
                GoldPercentCash = e.GoldPercentCash,
                PlatinumPercentCash = e.PlatinumPercentCash,
                BronzeColor = e.BronzeColor,
                SilverColor = e.SilverColor,
                GoldColor = e.GoldColor,
                PlatinumColor = e.PlatinumColor,
                GoldIconUrl = e.GoldIconUrl,
                GoldThreshold = e.GoldThreshold,
                PlatinumIconUrl = e.PlatinumIconUrl,
                PlatinumThreshold = e.PlatinumThreshold,
                BackgroundColor = e.BackgroundColor,
                ForegroundColor = e.ForegroundColor,
                LabelColor = e.LabelColor,
                ProgressBarColor = e.ProgressBarColor,
                LogoUrl = e.LogoUrl,
                GoogleLogoUrl = e.GoogleLogoUrl,
                OneRedeem = e.OneRedeem,
                PayMentType = e.PayMentType,
                BackgroundImageUrl = e.BackgroundImageUrl,
                StripImageUrl = e.StripImageUrl,
                HeroImageUrl = e.HeroImageUrl,
                AppLogoUrl = e.AppLogoUrl,
                AppStripUrl = e.AppStripUrl,
                Currency = e.Currency,
                IconLayout = e.IconLayout,
                Design = CardDesignSettings.Parse(e.DesignJson),
                FreeRewardLabel = e.FreeRewardLabel,

            };
        }
        public async Task<ServiceResult> Delete(int id)
        {
            var template = await _repo.GetById(id);
            if (template == null)
                return new ServiceResult(false, "Template not found");

            template.IsActive = false;
            _unitOfWork.Repository<DataAccess.Entities.Main.CardTemplate>().Update(template);
            await _unitOfWork.CommitAsync();
            return new ServiceResult(true);
        }

        public async Task<ServiceResult> Get(int id)
        {
            var respose = await _repo.GetById(id);
            var response = _mapper.Map<GetCardTemplateDto>(respose);
            return new ServiceResult(true, response);
        }

        public async Task<ServiceResult> GetAll()
        {
            var partners = await _unitOfWork.Repository<CardTemplate>().GetAllAsync();
            if (partners != null)
            {
                var response = _mapper.Map<ICollection<GetCardTemplateDto>>(partners);
                return new ServiceResult(true, response);
            }

            return new ServiceResult(false);
        }

        public Task<ServiceResult> GetLast()
        {
            throw new NotImplementedException();
        }

        public Task<ServiceResult> Update(AddCardTemplateDto dto, int id)
        {
            throw new NotImplementedException();
        }
    }
}

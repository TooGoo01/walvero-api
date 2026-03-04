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
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
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
        private readonly ICardTemplateIconService _cardTemplateIconService;
        private readonly IStorage _storage;

        public CardTemplateService(IStorage storage, ICardTemplateRepository repo, IMapper mapper, IUnitOfWork unitOfWork, ICardTemplateIconService cardTemplateIconService)
        {
            _storage = storage;
            _repo = repo;
            _mapper = mapper;
            _unitOfWork = unitOfWork;
            _cardTemplateIconService = cardTemplateIconService;
        }

        public Task<ServiceResult> Add(AddCardTemplateDto Dto)
        {
            throw new NotImplementedException();
        }

        public async Task<ServiceResult> AddOrUpdateAsync(AddCardTemplateDto dto, CancellationToken ct = default)
        {
            if (dto is null) return new ServiceResult(false, "Model is null.");
            if (dto.LoyaltyProgramId <= 0) return new ServiceResult(false, "LoyaltyProgramId is required.");
            if (string.IsNullOrWhiteSpace(dto.TemplateName)) return new ServiceResult(false, "TemplateName is required.");

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

            // Points-based icon faylları (bronze/silver/gold/platinum)
            entity.BronzeIconUrl = await UpOrNullLink(container, "bronze", dto.BronzeIconFile, dto.BronzeIconUrl, entity.BronzeIconUrl, ct);
            entity.SilverIconUrl = await UpOrNullLink(container, "silver", dto.SilverIconFile, dto.SilverIconUrl, entity.SilverIconUrl, ct);
            entity.GoldIconUrl = await UpOrNullLink(container, "gold", dto.GoldIconFile, dto.GoldIconUrl, entity.GoldIconUrl, ct);
            entity.PlatinumIconUrl = await UpOrNullLink(container, "platinum", dto.PlatinumIconFile, dto.PlatinumIconUrl, entity.PlatinumIconUrl, ct);

            // 4) Digər sahələr
            entity.TemplateName = dto.TemplateName.Trim();
            entity.TemplateType = dto.TemplateType;
            entity.TotalSteps = Math.Max(1, dto.TotalSteps);
            entity.StepLabel = dto.StepLabel?.Trim() ?? "Step";
            entity.RewardDescription = dto.RewardDescription?.Trim() ?? "";
            entity.FreeRewardLabel = dto.FreeRewardLabel?.Trim();  // nullable, default "Pulsuz içki" istifadəçi tərəfində

            entity.BronzeThreshold = dto.BronzeThreshold;
            entity.BronzePercent = dto.BronzePercent;
            entity.SilverThreshold = dto.SilverThreshold;
            entity.SilverPercent = dto.SilverPercent;
            entity.GoldThreshold = dto.GoldThreshold;
            entity.GoldPercent = dto.GoldPercent;
            entity.PlatinumThreshold = dto.PlatinumThreshold;
            entity.PlatinumPercent = dto.PlatinumPercent;
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

            // 5) PROGRESS ikonları: AddRangeAsync köhnələri silir, yenilərini blob-a yükləyir

            // 6) Yadda saxla
            if (entity.Id == 0)
                await _unitOfWork.Repository<CardTemplate>().AddAsync(entity);
            else
                _unitOfWork.Repository<CardTemplate>().Update(entity);
            _unitOfWork.Commit();
            await _cardTemplateIconService.AddRangeAsync(dto, entity.Id, ct);
            // local obyekt üçün
            // 7) Cavab
            var result = MapToGetDto(entity);
            return new ServiceResult(true, result);
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
                    IconUrl = x.IconUrl,
                    Order = x.Order
                }).ToList() ?? new List<IconDto>(),
                TotalSteps = e.TotalSteps,
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
                BackgroundImageUrl = e.BackgroundImageUrl,
                StripImageUrl = e.StripImageUrl,
                HeroImageUrl = e.HeroImageUrl,
                Currency = e.Currency,
                IconLayout = e.IconLayout,
                FreeRewardLabel = e.FreeRewardLabel,
               
            };
        }
        public Task<ServiceResult> Delete(int id)
        {
            throw new NotImplementedException();
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

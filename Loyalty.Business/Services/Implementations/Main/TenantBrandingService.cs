using AutoMapper;
using Loyalty.Business.Dtos.Get.Main;
using Loyalty.Business.Dtos.Post.Main;
using Loyalty.Business.Results;
using Loyalty.Business.Services.Abstractions.Main;
using Loyalty.Business.Services.Abstractions.Storage;
using Loyalty.DataAccess.Entities.Main;
using Loyalty.DataAccess.Repositories.Abstractions.Main;
using Loyalty.DataAccess.Repositories.Implementations.Main;
using Loyalty.DataAccess.UnitOfWorks;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.Net.WebRequestMethods;

namespace Loyalty.Business.Services.Implementations.Main
{
    public class TenantBrandingService : ITenantBrandingService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;
        private readonly ITenantBrandingRepository _repo;
        private readonly IStorage _storage;
        private readonly IMediaProcessor _mediaProcessor;


        public TenantBrandingService(ITenantBrandingRepository repo, IMapper mapper, IUnitOfWork unitOfWork, IStorage storage, IMediaProcessor mediaProcessor)
        {
            _repo = repo;
            _mapper = mapper;
            _unitOfWork = unitOfWork;
            _storage = storage;
            _mediaProcessor = mediaProcessor;
        }





        public Task<ServiceResult> Add(AddTenantBrandingDto Dto)
        {
            throw new NotImplementedException();
        }

        public async Task<ServiceResult> AddOrUpdateAsync(AddTenantBrandingDto dto, CancellationToken ct = default)
        {
           

            // 2) Mövcud branding
            var branding = await _repo.GetByTenantIdAsync(dto.TenantId, ct) ?? new TenantBranding { TenantId = dto.TenantId };

            // Upload üçün konteyner yolu (nisbi): uploads/tenant-{id}/branding
            var container = $"tenant-{dto.TenantId}-branding";

            // 3) LOGO (convert to WebP)
            if (dto.LogoFile is not null && dto.LogoFile.Length > 0)
            {
                if (_mediaProcessor.IsImage(dto.LogoFile))
                    dto.LogoFile = await _mediaProcessor.ConvertImageToWebpAsync(dto.LogoFile);

                if (!string.IsNullOrWhiteSpace(branding.LogoUrl))
                {
                    var oldLogoName = GetFileNameSafe(branding.LogoUrl);
                    if (!string.IsNullOrEmpty(oldLogoName))
                        await _storage.DeleteAsync(container, oldLogoName);
                }

                var uploaded = await _storage.UploadFileAsync(container, new[] { dto.LogoFile });  // <= array implements IEnumerable<IFormFile>
                var (_, relativePath) = uploaded.First();
                branding.LogoUrl = EnsureRelative(relativePath);
            }
            else if (!string.IsNullOrWhiteSpace(dto.LogoUrl))
            {
                // Fayl yox, URL verilibsə qəbul et (external ola bilər)
                branding.LogoUrl = dto.LogoUrl.Trim();
            }

            // 4) BACKGROUND
            if (dto.BackgroundImageFile is not null && dto.BackgroundImageFile.Length > 0)
            {
                if (!string.IsNullOrWhiteSpace(branding.BackgroundImageUrl))
                {
                    var oldBgName = GetFileNameSafe(branding.BackgroundImageUrl);
                    if (!string.IsNullOrEmpty(oldBgName))
                        await _storage.DeleteAsync(container, oldBgName);
                }

                var uploaded = await _storage.UploadFileAsync(container, new[] { dto.BackgroundImageFile }); // <= same idea
                var (_, relativePath) = uploaded.First();
                branding.BackgroundImageUrl = EnsureRelative(relativePath);
            }
            else if (!string.IsNullOrWhiteSpace(dto.BackgroundImageUrl))
            {
                branding.BackgroundImageUrl = dto.BackgroundImageUrl.Trim();
            }

            // 5) Digər sahələr
            branding.PrimaryColor = string.IsNullOrWhiteSpace(dto.PrimaryColor) ? "#000000" : dto.PrimaryColor!;
            branding.SecondaryColor = string.IsNullOrWhiteSpace(dto.SecondaryColor) ? "#FFFFFF" : dto.SecondaryColor!;

            branding.Description = dto.Description;
            branding.Website = dto.Website;

            // 6) Yadda saxla
            if (branding.Id == 0) await _unitOfWork.Repository<TenantBranding>().AddAsync(branding);
            else _repo.Update(branding);

            _unitOfWork.Commit();

            // 7) Cavab
           var br= new GetTenantBrandingDto
            {
                Id = branding.Id,
                LogoUrl = branding.LogoUrl,
                BackgroundImageUrl = branding.BackgroundImageUrl,
                PrimaryColor = branding.PrimaryColor,
                SecondaryColor = branding.SecondaryColor,
                Description = branding.Description,
                Website = branding.Website
            };
            return new ServiceResult(true,br);
        }
        private static string GetFileNameSafe(string? urlOrPath)
        {
            if (string.IsNullOrWhiteSpace(urlOrPath)) return "";
            try
            {
                // absolute və ya nisbi fərq etməz — sadəcə fayl adını çıxar
                var uriOk = Uri.TryCreate(urlOrPath, UriKind.Absolute, out var uri);
                var path = uriOk ? uri!.AbsolutePath : urlOrPath;
                return Path.GetFileName(path);
            }
            catch { return ""; }
        }

        private static string EnsureRelative(string pathOrUrl)
        {
            // Storage nisbi qaytarırsa olduğu kimi saxla; absolute-dursa nisbiləşdirmək istəmirsənsə, olduğu kimi qoy
            if (pathOrUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase)) return pathOrUrl;
            return pathOrUrl.TrimStart('/'); // "uploads/..." formatında qalsın
        }

        public async Task<ServiceResult> Delete(int id)
        {
            var entity = await _unitOfWork.Repository<TenantBranding>().GetByIdAsync(id);
            if (entity == null)
                return new ServiceResult(false, "Tenant branding not found.");

            _unitOfWork.Repository<TenantBranding>().Delete(entity);
            _unitOfWork.Commit();

            return new ServiceResult(true, "Tenant branding deleted successfully.");
        }

        public async Task<ServiceResult> Get(int id)
        {
            var respose = await _repo.GetById(id);
            var response = _mapper.Map<GetTenantBrandingDto>(respose);
            return new ServiceResult(true, response);
        }

        public async Task<ServiceResult> GetAll()
        {
            var partners = await _unitOfWork.Repository<TenantBranding>().GetAllAsync();
            if (partners != null)
            {
                var response = _mapper.Map<ICollection<GetTenantBrandingDto>>(partners);
                return new ServiceResult(true, response);
            }

            return new ServiceResult(false);
        }

        public Task<ServiceResult> GetLast()
        {
            throw new NotImplementedException();
        }

        public async Task<ServiceResult> Update(AddTenantBrandingDto dto, int id)
        {
            throw new NotImplementedException();
        }

        public async Task<ServiceResult> GetByTenantId(int id)
        {
            var respose = await _repo.GetByTenantIdAsync(id);
            var response = _mapper.Map<GetTenantBrandingDto>(respose);
            return new ServiceResult(true, response);
        }
    }
}

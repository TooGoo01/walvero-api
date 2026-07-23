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
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Webp;
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

            // 3) LOGO (.webp-yə çevir)
            if (dto.LogoFile is not null && dto.LogoFile.Length > 0)
            {
                if (_mediaProcessor.IsImage(dto.LogoFile))
                    dto.LogoFile = await _mediaProcessor.ConvertImageToWebpAsync(dto.LogoFile);

                if (!string.IsNullOrWhiteSpace(branding.LogoUrl))
                {
                    var oldLogoName = GetFileNameSafe(branding.LogoUrl);
                    if (!string.IsNullOrEmpty(oldLogoName))
                        try { await _storage.DeleteAsync(container, oldLogoName); } catch { }
                }

                branding.LogoUrl = await UploadAsWebp(container, "logo", dto.LogoFile);
            }
            else if (!string.IsNullOrWhiteSpace(dto.LogoUrl))
            {
                branding.LogoUrl = dto.LogoUrl.Trim();
            }

            // 4) BACKGROUND (.webp-yə çevir)
            if (dto.BackgroundImageFile is not null && dto.BackgroundImageFile.Length > 0)
            {
                if (!string.IsNullOrWhiteSpace(branding.BackgroundImageUrl))
                {
                    var oldBgName = GetFileNameSafe(branding.BackgroundImageUrl);
                    if (!string.IsNullOrEmpty(oldBgName))
                        try { await _storage.DeleteAsync(container, oldBgName); } catch { }
                }

                branding.BackgroundImageUrl = await UploadAsWebp(container, "bg", dto.BackgroundImageFile);
            }
            else if (!string.IsNullOrWhiteSpace(dto.BackgroundImageUrl))
            {
                branding.BackgroundImageUrl = dto.BackgroundImageUrl.Trim();
            }

            // 5) Digər sahələr
            branding.PrimaryColor = string.IsNullOrWhiteSpace(dto.PrimaryColor) ? "#000000" : dto.PrimaryColor!;
            branding.SecondaryColor = string.IsNullOrWhiteSpace(dto.SecondaryColor) ? "#FFFFFF" : dto.SecondaryColor!;
            branding.BackgroundColor = string.IsNullOrWhiteSpace(dto.BackgroundColor) ? "#FFFFFF" : dto.BackgroundColor!;
            branding.TextColor = string.IsNullOrWhiteSpace(dto.TextColor) ? "#000000" : dto.TextColor!;

            branding.Description = dto.Description;
            branding.Website = dto.Website;

            branding.BranchNameFontSize = dto.BranchNameFontSize is int fs
                ? Math.Clamp(fs, 10, 40) : (int?)null;
            branding.DefaultMenuView = dto.DefaultMenuView?.Trim().ToLowerInvariant() switch
            {
                "grid" => "grid",
                "list" => "list",
                _ => null
            };

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
                BackgroundColor = branding.BackgroundColor,
                TextColor = branding.TextColor,
                Description = branding.Description,
                Website = branding.Website,
                BranchNameFontSize = branding.BranchNameFontSize,
                DefaultMenuView = branding.DefaultMenuView
            };
            return new ServiceResult(true,br);
        }
        private async Task<string> UploadAsWebp(string container, string prefix, IFormFile file)
        {
            using var inputStream = file.OpenReadStream();
            using var image = await Image.LoadAsync(inputStream);
            using var webpStream = new MemoryStream();
            await image.SaveAsWebpAsync(webpStream, new WebpEncoder { Quality = 80 });
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

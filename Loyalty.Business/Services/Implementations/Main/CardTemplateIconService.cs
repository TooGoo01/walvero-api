using AutoMapper;
using Loyalty.Business.Dtos.Post.Main;
using Loyalty.Business.Results;
using Loyalty.Business.Services.Abstractions.Main;
using Loyalty.Business.Services.Abstractions.Storage;
using Loyalty.DataAccess.Entities.Main;
using Loyalty.DataAccess.UnitOfWorks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading.Tasks;

namespace Loyalty.Business.Services.Implementations.Main
{
    public class CardTemplateIconService : ICardTemplateIconService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IStorage _storage;
        private readonly IMapper _mapper;

        public CardTemplateIconService(IMapper mapper, IStorage storage, IUnitOfWork unitOfWork)
        {
            _mapper = mapper;
            _storage = storage;
            _unitOfWork = unitOfWork;
        }

        public async Task<ServiceResult> AddRangeAsync(AddCardTemplateDto dto, int templateId, CancellationToken ct = default)
        {
            if (dto is null) return new ServiceResult(false, "Model is null.");

            // Yeni icon göndərilməyibsə, köhnələrə toxunma
            if (dto.Icons == null || dto.Icons.Count == 0)
                return new ServiceResult(true, "No new icons provided, existing icons preserved.");

            var template = _unitOfWork.Repository<CardTemplate>().Get(x => x.Id == templateId);
            if (template is null) return new ServiceResult(false, "CardTemplate not found.");

            var container = $"photo-{templateId}";

            // Köhnə ikonları blobdan sil və DB-dən təmizlə
            var existingIcons = (await _unitOfWork.Repository<Icon>()
                .GetAllAsync(x => x.CardTemplateId == templateId))?.ToList();

            if (existingIcons is { Count: > 0 })
            {
                foreach (var old in existingIcons)
                {
                    // Yalnız blob-da saxlanılan faylları sil (external URL-ləri yox)
                    if (!string.IsNullOrWhiteSpace(old.FileName) &&
                        (string.IsNullOrWhiteSpace(old.Path) || !old.Path.StartsWith("http", StringComparison.OrdinalIgnoreCase)))
                    {
                        try { await _storage.DeleteAsync(container, old.FileName); }
                        catch { /* blob yoxdursa keç */ }
                    }
                }
                _unitOfWork.Repository<Icon>().DeleteRange(existingIcons);
                _unitOfWork.Commit();
            }

            var newIcons = new List<Icon>();

            // Sıralama (əgər IconDto.Order varsa onunla, yoxdursa gələn sıra ilə)
            var iconsOrdered = dto.Icons?.OrderBy(i => i.Order).ToList() ?? new List<IconDto>();
            var order = 0;

            foreach (var it in iconsOrdered)
            {
                // 1) Fayl varsa yüklə
                if (it.IconFile is not null && it.IconFile.Length > 0)
                {
                    var uploaded = await _storage.UploadFileAsync(container, new[] { it.IconFile }); // IEnumerable<IFormFile>
                    var (fileName, relPath) = uploaded.First();

                    newIcons.Add(new Icon
                    {
                        FileName = fileName,
                        Path = EnsureRelative(relPath),
                        // varsa bu sahələr:
                        Name = it.Name,
                        Order = order,
                        CardTemplate = template
                    });
                    order++;
                    continue;
                }

                // 2) Fayl yoxdursa, URL varsa birbaşa saxla
                if (!string.IsNullOrWhiteSpace(it.IconUrl))
                {
                    var url = it.IconUrl.Trim();
                    newIcons.Add(new Icon
                    {
                        FileName = TryGetFileName(url) ?? $"icon_{order}",
                        Path = url, // external/internal URL ola bilər
                        Name = it.Name,
                        Order = order,
                        CardTemplate = template
                    });
                    order++;
                    continue;
                }

                // 3) Həm file, həm də url boşdursa — keç
            }

            if (newIcons.Count == 0) return new ServiceResult(false, "No icons to add.");

            _unitOfWork.Repository<Icon>().AddRange(newIcons);
            _unitOfWork.Commit(); // və ya await CommitAsync(ct)

            return new ServiceResult(true);
        }

        private static string EnsureRelative(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return path;
            var p = path.Replace("\\", "/");
            var i = p.IndexOf("/resource/", StringComparison.OrdinalIgnoreCase);
            if (i >= 0) return p[i..];
            i = p.IndexOf("/uploads/", StringComparison.OrdinalIgnoreCase);
            return i >= 0 ? p[i..] : p.TrimStart('/');
        }

        private static string? TryGetFileName(string urlOrPath)
        {
            if (string.IsNullOrWhiteSpace(urlOrPath)) return null;
            try
            {
                if (Uri.TryCreate(urlOrPath, UriKind.Absolute, out var u))
                    return System.IO.Path.GetFileName(u.LocalPath);
            }
            catch { /* ignore */ }

            return System.IO.Path.GetFileName(urlOrPath.Replace("\\", "/"));
        }

        public Task<ServiceResult> UpdateRangeAsync(AddCardTemplateDto addLoyaltyProgramDto, int aboutId)
        {
            throw new NotImplementedException();
        }
    }
}

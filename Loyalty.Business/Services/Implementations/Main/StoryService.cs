using AutoMapper;
using Loyalty.Business.Dtos.Get.Main;
using Loyalty.Business.Dtos.Post.Main;
using Loyalty.Business.Results;
using Loyalty.Business.Services.Abstractions.Main;
using Loyalty.Business.Services.Abstractions.Storage;
using Loyalty.DataAccess.Entities.Main;
using Loyalty.DataAccess.Repositories.Abstractions.Main;
using Loyalty.DataAccess.UnitOfWorks;

using Microsoft.AspNetCore.Http;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Webp;

namespace Loyalty.Business.Services.Implementations.Main
{
    public class StoryService : IStoryService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;
        private readonly IStoryRepository _storyRepository;
        private readonly IStorage _storage;
        private readonly IMediaProcessor _mediaProcessor;

        public StoryService(IStoryRepository storyRepository, IMapper mapper, IUnitOfWork unitOfWork, IStorage storage, IMediaProcessor mediaProcessor)
        {
            _storyRepository = storyRepository;
            _mapper = mapper;
            _unitOfWork = unitOfWork;
            _storage = storage;
            _mediaProcessor = mediaProcessor;
        }

        public async Task<ServiceResult> AddStory(AddStoryDto story)
        {
            try
            {
                var request = _mapper.Map<Story>(story);
                request.IsActive = true;

                // Handle file uploads
                var container = $"stories-tenant-{story.TenantId ?? 0}";
                request.SmallImageUrl = await UploadOrUseUrl(container, "small", story.SmallImageFile, story.SmallImageUrl, null);
                request.ImageUrl = await UploadOrUseUrl(container, "image", story.ImageFile, story.ImageUrl, null);
                await ApplyVideoUpload(request, container, story.VideoFile, story.VideoUrl);

                await _unitOfWork.Repository<Story>().AddAsync(request);
                _unitOfWork.Commit();
                var response = _mapper.Map<GetStoryDto>(request);
                return new ServiceResult(true, response.Id);
            }
            catch (InvalidOperationException ex)
            {
                // Validation errors (məs. video > 60s) admin-friendly mesajla qaytarılır
                return new ServiceResult(false, ex.Message);
            }
        }

        public async Task<ServiceResult> DeleteStory(int id)
        {
            var story = await _storyRepository.GetStory(id);
            if (story != null)
            {
                story.IsActive = false;
                _unitOfWork.Repository<Story>().Update(story);
                _unitOfWork.Commit();
                return new ServiceResult(true);
            }
            return new ServiceResult(false);
        }

        public async Task<ServiceResult> GetActiveStories()
        {
            var stories = await _storyRepository.GetActiveStories();
            if (stories != null)
            {
                var response = _mapper.Map<ICollection<GetStoryDto>>(stories);
                return new ServiceResult(true, response);
            }
            return new ServiceResult(false);
        }

        public async Task<ServiceResult> GetStories()
        {
            var stories = await _storyRepository.GetAllStories();
            if (stories != null)
            {
                var response = _mapper.Map<ICollection<GetStoryDto>>(stories);
                return new ServiceResult(true, response);
            }
            return new ServiceResult(false);
        }

        public async Task<ServiceResult> GetStory(int id)
        {
            try
            {
                var result = await _storyRepository.GetStory(id);
                if (result == null)
                    return new ServiceResult(false, "data not found");

                var response = _mapper.Map<GetStoryDto>(result);
                return new ServiceResult(true, response);
            }
            catch (Exception ex)
            {
                return new ServiceResult(false, ex.Message);
            }
        }

        public async Task<ServiceResult> GetStoriesByTenant(int tenantId)
        {
            var stories = await _storyRepository.GetStoriesByTenant(tenantId);
            if (stories != null)
            {
                var response = _mapper.Map<ICollection<GetStoryDto>>(stories);
                return new ServiceResult(true, response);
            }
            return new ServiceResult(false);
        }

        public async Task<ServiceResult> UpdateStory(AddStoryDto storyDto, int id)
        {
            try
            {
                var story = await _storyRepository.GetStory(id);
                if (story != null)
                {
                    if (!string.IsNullOrWhiteSpace(storyDto.AzTitle))
                    {
                        story.AzTitle = storyDto.AzTitle.Trim();
                    }

                    if (!string.IsNullOrWhiteSpace(storyDto.EnTitle))
                    {
                        story.EnTitle = storyDto.EnTitle.Trim();
                    }

                    if (!string.IsNullOrWhiteSpace(storyDto.RuTitle))
                    {
                        story.RuTitle = storyDto.RuTitle.Trim();
                    }

                    if (storyDto.TenantId.HasValue)
                    {
                        story.TenantId = storyDto.TenantId;
                    }

                    // Handle file uploads
                    var container = $"stories-tenant-{storyDto.TenantId ?? story.TenantId ?? 0}";
                    story.SmallImageUrl = await UploadOrUseUrl(container, "small", storyDto.SmallImageFile, storyDto.SmallImageUrl, story.SmallImageUrl);
                    story.ImageUrl = await UploadOrUseUrl(container, "image", storyDto.ImageFile, storyDto.ImageUrl, story.ImageUrl);
                    await ApplyVideoUpload(story, container, storyDto.VideoFile, storyDto.VideoUrl);

                    _unitOfWork.Repository<Story>().Update(story);
                    _unitOfWork.Commit();
                    var response = _mapper.Map<GetStoryDto>(story);
                    return new ServiceResult(true, response.Id);
                }
                return new ServiceResult(false);
            }
            catch (InvalidOperationException ex)
            {
                // Validation errors (məs. video > 60s) admin-friendly mesajla qaytarılır
                return new ServiceResult(false, ex.Message);
            }
        }

        private async Task<string?> UploadOrUseUrl(string container, string prefix, IFormFile? file, string? url, string? oldValue)
        {
            if (file is not null && file.Length > 0)
            {
                if (!string.IsNullOrWhiteSpace(oldValue))
                {
                    var oldName = GetFileNameSafe(oldValue);
                    if (!string.IsNullOrEmpty(oldName))
                        try { await _storage.DeleteAsync(container, oldName); } catch { }
                }

                // Şəkil yolu: .webp-yə çevir
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
            else if (!string.IsNullOrWhiteSpace(url))
            {
                return url.Trim();
            }
            return oldValue;
        }

        /// <summary>
        /// Story video upload yolu:
        ///  - Yeni fayl gəlibsə: köhnə video + poster blob silinir, yeni video `UploadProcessedVideoAsync`
        ///    ilə +faststart + cache header-ları ilə yüklənir, paralel olaraq poster.jpg generator olunur.
        ///  - URL stringi gəlibsə birbaşa saxlanır, poster sahələri toxunulmaz qalır.
        ///  - Heç biri yoxdursa mövcud dəyərlər saxlanır.
        /// </summary>
        private async Task ApplyVideoUpload(Story story, string container, IFormFile? file, string? url)
        {
            if (file is not null && file.Length > 0)
            {
                if (!string.IsNullOrWhiteSpace(story.VideoUrl))
                {
                    var oldName = GetFileNameSafe(story.VideoUrl);
                    if (!string.IsNullOrEmpty(oldName))
                        try { await _storage.DeleteAsync(container, oldName); } catch { }
                }
                if (!string.IsNullOrWhiteSpace(story.VideoPosterFileName))
                {
                    try { await _storage.DeleteAsync(container, story.VideoPosterFileName); } catch { }
                }

                var uploaded = await _storage.UploadProcessedVideoAsync(container, file);
                story.VideoUrl = EnsureRelative(uploaded.VideoUrl);
                story.VideoPosterUrl = uploaded.PosterUrl is null ? null : EnsureRelative(uploaded.PosterUrl);
                story.VideoPosterFileName = uploaded.PosterFileName;
            }
            else if (!string.IsNullOrWhiteSpace(url))
            {
                story.VideoUrl = url.Trim();
            }
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
    }
}

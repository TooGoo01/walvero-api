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
            var request = _mapper.Map<Story>(story);
            request.IsActive = true;

            // Handle file uploads
            var container = $"stories-tenant-{story.TenantId ?? 0}";
            request.SmallImageUrl = await UploadOrUseUrl(container, "small", story.SmallImageFile, story.SmallImageUrl, null);
            request.ImageUrl = await UploadOrUseUrl(container, "image", story.ImageFile, story.ImageUrl, null);
            request.VideoUrl = await UploadOrUseUrl(container, "video", story.VideoFile, story.VideoUrl, null);

            await _unitOfWork.Repository<Story>().AddAsync(request);
            _unitOfWork.Commit();
            var response = _mapper.Map<GetStoryDto>(request);
            return new ServiceResult(true, response.Id);
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
                story.VideoUrl = await UploadOrUseUrl(container, "video", storyDto.VideoFile, storyDto.VideoUrl, story.VideoUrl);

                _unitOfWork.Repository<Story>().Update(story);
                _unitOfWork.Commit();
                var response = _mapper.Map<GetStoryDto>(story);
                return new ServiceResult(true, response.Id);
            }
            return new ServiceResult(false);
        }

        private async Task<string?> UploadOrUseUrl(string container, string prefix, IFormFile? file, string? url, string? oldValue)
        {
            if (file is not null && file.Length > 0)
            {
                // Convert image to WebP
                if (_mediaProcessor.IsImage(file))
                    file = await _mediaProcessor.ConvertImageToWebpAsync(file);

                // Delete old file if exists
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
            return oldValue;
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

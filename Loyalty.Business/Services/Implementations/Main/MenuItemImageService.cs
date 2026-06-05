using AutoMapper;
using Loyalty.Business.Dtos.Get.Main;
using Loyalty.Business.Dtos.Post.Main;
using Loyalty.Business.Results;
using Loyalty.Business.Services.Abstractions.Main;
using Loyalty.Business.Services.Abstractions.Storage;
using Loyalty.Business.Services.Abstractions.Storage.Azure;
using Loyalty.DataAccess.Entities.Main;
using Loyalty.DataAccess.Repositories.Abstractions.Main;
using Loyalty.DataAccess.UnitOfWorks;
using Microsoft.AspNetCore.Http;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Loyalty.Business.Services.Implementations.Main
{
    public class MenuItemImageService : IMenuItemImageService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IAzureStorage _storage;
        private readonly IMapper _mapper;

        public MenuItemImageService(IMapper mapper, IAzureStorage storage = null, IUnitOfWork unitOfWork = null)
        {
            _mapper = mapper;
            _storage = storage;
            _unitOfWork = unitOfWork;
        }

        public async Task<ServiceResult> AddRangeAsync(AddMenuItemDto aboutDto, int aboutId)
        {
            List<(string fileName, string pathOrContainerName)> result = await _storage.UploadAsync("photo-menu", aboutDto.Files);
            var about = _unitOfWork.Repository<MenuItem>().Get(x => x.Id == aboutId);
            _unitOfWork.Repository<MenuItemImage>().AddRange(result.Select(x => new MenuItemImage
            {
                FileName = x.fileName,
                Path = x.pathOrContainerName,
                MenuItem = about
            }));
            _unitOfWork.Commit();
            return new ServiceResult(true);
        }

        public async Task<ServiceResult> UpdateRangeAsync(AddMenuItemDto aboutDto, int aboutId)
        {
            const string containerName = "photo-menu";
            var about = _unitOfWork.Repository<MenuItem>().Get(x => x.Id == aboutId);
            var existingImages = await _unitOfWork.Repository<MenuItemImage>().GetAllAsync(x => x.MenuItemId == aboutId);

            // Köhnə şəkilləri storage-dan sil
            if (_storage != null)
            {
                foreach (var img in existingImages)
                {
                    if (!string.IsNullOrWhiteSpace(img.FileName))
                    {
                        try
                        {
                            await _storage.DeleteAsync(containerName, img.FileName);
                        }
                        catch { /* şəkil tapılmadı və ya silinə bilmədi - davam et */ }
                    }
                }
            }

            // Köhnə şəkilləri DB-dən sil
            _unitOfWork.Repository<MenuItemImage>().DeleteRange(existingImages);

            // Yeni şəkilləri yüklə və əlavə et
            List<(string fileName, string pathOrContainerName)> result = await _storage.UploadAsync(containerName, aboutDto.Files);
            _unitOfWork.Repository<MenuItemImage>().AddRange(result.Select(x => new MenuItemImage
            {
                FileName = x.fileName,
                Path = x.pathOrContainerName,
                MenuItem = about
            }));

            _unitOfWork.Commit();
            return new ServiceResult(true);
        }

        public async Task<ServiceResult> UploadIngredientVideoAsync(int menuItemId, IFormFile? videoFile, bool remove)
        {
            const string container = "video-menu";
            var item = _unitOfWork.Repository<MenuItem>().Get(x => x.Id == menuItemId);
            if (item == null)
                return new ServiceResult(false, "Menu item tapılmadı");

            var shouldClearExisting = (videoFile != null && videoFile.Length > 0) || remove;
            if (shouldClearExisting && !string.IsNullOrWhiteSpace(item.IngredientVideoFileName) && _storage != null)
            {
                try
                {
                    await _storage.DeleteAsync(container, item.IngredientVideoFileName);
                }
                catch
                {
                    // storage-da fayl tapılmadı və ya silinə bilmədi — davam et
                }

                item.IngredientVideoUrl = null;
                item.IngredientVideoFileName = null;
            }

            if (videoFile != null && videoFile.Length > 0)
            {
                var collection = new FormFileCollection { videoFile };
                var uploaded = await _storage.UploadAsync(container, collection);
                var first = uploaded.FirstOrDefault();
                if (!string.IsNullOrWhiteSpace(first.fileName))
                {
                    item.IngredientVideoFileName = first.fileName;
                    item.IngredientVideoUrl = first.pathOrContainerName;
                }
            }

            _unitOfWork.Repository<MenuItem>().Update(item);
            _unitOfWork.Commit();
            return new ServiceResult(true);
        }
    }
}

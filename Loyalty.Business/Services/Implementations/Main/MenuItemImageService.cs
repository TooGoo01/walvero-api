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
using System.Collections.Generic;
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
            List<(string fileName, string pathOrContainerName)> result = await _storage.UploadAsync("photo-menu", aboutDto.Files);
            var about = _unitOfWork.Repository<MenuItem>().Get(x => x.Id == aboutId);
            var entity = await _unitOfWork.Repository<MenuItemImage>().GetAllAsync(x => x.MenuItem.Id == aboutId);
            _unitOfWork.Repository<MenuItemImage>().DeleteRange(entity);
            _unitOfWork.Repository<MenuItemImage>().UpdateRange(result.Select(x => new MenuItemImage
            {
                FileName = x.fileName,
                Path = x.pathOrContainerName,
                MenuItem = about
            }));
            _unitOfWork.Commit();
            return new ServiceResult(true);
        }
    }
}

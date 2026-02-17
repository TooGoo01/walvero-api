using AutoMapper;
using Loyalty.Business.Dtos.Get.Main;
using Loyalty.Business.Dtos.Post.Main;
using Loyalty.Business.Results;
using Loyalty.Business.Services.Abstractions.Main;
using Loyalty.DataAccess.Entities.Main;
using Loyalty.DataAccess.Repositories.Abstractions.Main;
using Loyalty.DataAccess.UnitOfWorks;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Loyalty.Business.Services.Implementations.Main
{
    public class MenuItemService : IMenuItemService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;
        private readonly IMenuItemRepository _menuItemRepository;

        public MenuItemService(
            IUnitOfWork unitOfWork,
            IMapper mapper,
            IMenuItemRepository menuItemRepository)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
            _menuItemRepository = menuItemRepository;
        }

        public async Task<ServiceResult> Add(AddMenuItemDto dto)
        {
            var entity = _mapper.Map<MenuItem>(dto);
            entity.IsActive = true;
            
            await _unitOfWork.Repository<MenuItem>().AddAsync(entity);
            _unitOfWork.Commit();

            var response = _mapper.Map<GetMenuItemDto>(entity);
            return new ServiceResult(true, response, "Menyu elementi uğurla əlavə edildi");
        }

        public async Task<ServiceResult> Update(UpdateMenuItemDto dto)
        {
            var entity = await _unitOfWork.Repository<MenuItem>().GetByIdAsync(dto.Id);
            if (entity == null)
                return new ServiceResult(false, "Menyu elementi tapılmadı");

            _mapper.Map(dto, entity);
            _unitOfWork.Repository<MenuItem>().Update(entity);
            _unitOfWork.Commit();

            var response = _mapper.Map<GetMenuItemDto>(entity);
            return new ServiceResult(true, response, "Menyu elementi uğurla yeniləndi");
        }

        public async Task<ServiceResult> Delete(int id)
        {
            var entity = await _unitOfWork.Repository<MenuItem>().GetByIdAsync(id);
            if (entity == null)
                return new ServiceResult(false, "Menyu elementi tapılmadı");

            entity.IsActive = false;
            _unitOfWork.Repository<MenuItem>().Update(entity);
            _unitOfWork.Commit();

            return new ServiceResult(true, "Menyu elementi uğurla silindi");
        }

        public async Task<ServiceResult> GetById(int id)
        {
            var entity = await _unitOfWork.Repository<MenuItem>().GetByIdAsync(id);
            if (entity == null)
                return new ServiceResult(false, "Menyu elementi tapılmadı");

            var response = _mapper.Map<GetMenuItemDto>(entity);
            return new ServiceResult(true, response);
        }

        public async Task<ServiceResult> GetByIdWithDetails(int id)
        {
            var entity = await _menuItemRepository.GetByIdWithDetailsAsync(id);
            if (entity == null)
                return new ServiceResult(false, "Menyu elementi tapılmadı");

            var response = _mapper.Map<GetMenuItemDto>(entity);
            return new ServiceResult(true, response);
        }

        public async Task<ServiceResult> GetByCategoryId(int categoryId)
        {
            var entities = await _menuItemRepository.GetByCategoryIdAsync(categoryId);
            var response = _mapper.Map<List<GetMenuItemDto>>(entities);
            return new ServiceResult(true, response);
        }

        public async Task<ServiceResult> GetAll()
        {
            var entities = await _menuItemRepository.GetActiveAsync();
            var response = _mapper.Map<List<GetMenuItemDto>>(entities);
            return new ServiceResult(true, response);
        }

        public async Task<ServiceResult> GetAllWithDetails()
        {
            var entities = await _menuItemRepository.GetAllWithDetailsAsync();
            var response = _mapper.Map<List<GetMenuItemDto>>(entities);
            return new ServiceResult(true, response);
        }

        public async Task<ServiceResult> GetActive()
        {
            var entities = await _menuItemRepository.GetActiveAsync();
            var response = _mapper.Map<List<GetMenuItemDto>>(entities);
            return new ServiceResult(true, response);
        }
    }
}

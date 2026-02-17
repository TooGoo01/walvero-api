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
    public class MenuCategoryService : IMenuCategoryService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;
        private readonly IMenuCategoryRepository _menuCategoryRepository;

        public MenuCategoryService(
            IUnitOfWork unitOfWork,
            IMapper mapper,
            IMenuCategoryRepository menuCategoryRepository)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
            _menuCategoryRepository = menuCategoryRepository;
        }

        public async Task<ServiceResult> Add(AddMenuCategoryDto dto)
        {
            var entity = _mapper.Map<MenuCategory>(dto);
            entity.IsActive = true;
            
            await _unitOfWork.Repository<MenuCategory>().AddAsync(entity);
            _unitOfWork.Commit();

            var response = _mapper.Map<GetMenuCategoryDto>(entity);
            return new ServiceResult(true, response, "Menyu kateqoriyası uğurla əlavə edildi");
        }

        public async Task<ServiceResult> Update(UpdateMenuCategoryDto dto)
        {
            var entity = await _unitOfWork.Repository<MenuCategory>().GetByIdAsync(dto.Id);
            if (entity == null)
                return new ServiceResult(false, "Menyu kateqoriyası tapılmadı");

            _mapper.Map(dto, entity);
            _unitOfWork.Repository<MenuCategory>().Update(entity);
            _unitOfWork.Commit();

            var response = _mapper.Map<GetMenuCategoryDto>(entity);
            return new ServiceResult(true, response, "Menyu kateqoriyası uğurla yeniləndi");
        }

        public async Task<ServiceResult> Delete(int id)
        {
            var entity = await _unitOfWork.Repository<MenuCategory>().GetByIdAsync(id);
            if (entity == null)
                return new ServiceResult(false, "Menyu kateqoriyası tapılmadı");

            entity.IsActive = false;
            _unitOfWork.Repository<MenuCategory>().Update(entity);
            _unitOfWork.Commit();

            return new ServiceResult(true, "Menyu kateqoriyası uğurla silindi");
        }

        public async Task<ServiceResult> GetById(int id)
        {
            var entity = await _unitOfWork.Repository<MenuCategory>().GetByIdAsync(id);
            if (entity == null)
                return new ServiceResult(false, "Menyu kateqoriyası tapılmadı");

            var response = _mapper.Map<GetMenuCategoryDto>(entity);
            return new ServiceResult(true, response);
        }

        public async Task<ServiceResult> GetByIdWithItems(int id)
        {
            var entity = await _menuCategoryRepository.GetByIdWithItemsAsync(id);
            if (entity == null)
                return new ServiceResult(false, "Menyu kateqoriyası tapılmadı");

            var response = _mapper.Map<GetMenuCategoryDto>(entity);
            return new ServiceResult(true, response);
        }

        public async Task<ServiceResult> GetAll()
        {
            var entities = await _menuCategoryRepository.GetActiveAsync();
            var response = _mapper.Map<List<GetMenuCategoryDto>>(entities);
            return new ServiceResult(true, response);
        }

        public async Task<ServiceResult> GetAllWithItems()
        {
            var entities = await _menuCategoryRepository.GetAllWithItemsAsync();
            var response = _mapper.Map<List<GetMenuCategoryDto>>(entities);
            return new ServiceResult(true, response);
        }

        public async Task<ServiceResult> GetActive()
        {
            var entities = await _menuCategoryRepository.GetActiveAsync();
            var response = _mapper.Map<List<GetMenuCategoryDto>>(entities);
            return new ServiceResult(true, response);
        }
    }
}

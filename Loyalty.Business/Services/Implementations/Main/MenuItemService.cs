using AutoMapper;
using Loyalty.Business.Dtos.Get.Main;
using Loyalty.Business.Dtos.Post.Main;
using Loyalty.Business.Results;
using Loyalty.Business.Services.Abstractions.Main;
using Loyalty.DataAccess.Context;
using Loyalty.DataAccess.Entities.Main;
using Loyalty.DataAccess.Repositories.Abstractions.Main;
using Loyalty.DataAccess.UnitOfWorks;
using Microsoft.EntityFrameworkCore;
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
        private readonly LoyaltyWalletDbContext _db;

        public MenuItemService(
            IUnitOfWork unitOfWork,
            IMapper mapper,
            IMenuItemRepository menuItemRepository,
            LoyaltyWalletDbContext db)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
            _menuItemRepository = menuItemRepository;
            _db = db;
        }

        public async Task<ServiceResult> Add(AddMenuItemDto dto)
        {
            var entity = _mapper.Map<MenuItem>(dto);
            entity.IsActive = true;

            var category = await _db.MenuCategories
                .AsNoTracking()
                .Where(mc => mc.Id == dto.MenuCategoryId)
                .Select(mc => new { mc.TenantId })
                .FirstOrDefaultAsync();
            if (category == null)
                return new ServiceResult(false, "Menyu kateqoriyası tapılmadı");

            entity.TenantId = category.TenantId;
            entity.BranchId = dto.BranchId;

            await _unitOfWork.Repository<MenuItem>().AddAsync(entity);
            _unitOfWork.Commit();

            var response = _mapper.Map<GetMenuItemDto>(entity);
            return new ServiceResult(true, response.Id, "Menyu elementi uğurla əlavə edildi");
        }

        public async Task<ServiceResult> Update(UpdateMenuItemDto dto)
        {
            var entity = await _unitOfWork.Repository<MenuItem>().GetByIdAsync(dto.Id);
            if (entity == null)
                return new ServiceResult(false, "Menyu elementi tapılmadı");

            _mapper.Map(dto, entity);

            if (entity.TenantId == null)
            {
                var category = await _db.MenuCategories
                    .AsNoTracking()
                    .Where(mc => mc.Id == entity.MenuCategoryId)
                    .Select(mc => new { mc.TenantId })
                    .FirstOrDefaultAsync();
                if (category != null) entity.TenantId = category.TenantId;
            }
            entity.BranchId = dto.BranchId;

            _unitOfWork.Repository<MenuItem>().Update(entity);
            _unitOfWork.Commit();

            var response = _mapper.Map<GetMenuItemDto>(entity);
            return new ServiceResult(true, response.Id, "Menyu elementi uğurla yeniləndi");
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

        public async Task<ServiceResult> GetByTenantAndBranchAsync(int tenantId, int? branchId, bool branchScoped, int? categoryId)
        {
            var query = _db.MenuItems
                .AsNoTracking()
                .Include(mi => mi.Branch)
                .Include(mi => mi.MenuItemImages)
                .Where(mi => mi.IsActive
                          && (mi.TenantId == tenantId
                              || mi.MenuCategory.TenantId == tenantId));

            if (branchScoped)
            {
                query = query.Where(mi => mi.BranchId == branchId);
            }
            else if (branchId.HasValue)
            {
                query = query.Where(mi => mi.BranchId == branchId.Value);
            }

            if (categoryId.HasValue)
            {
                query = query.Where(mi => mi.MenuCategoryId == categoryId.Value);
            }

            var entities = await query
                .OrderBy(mi => mi.MenuCategoryId)
                .ThenBy(mi => mi.SortOrder)
                .ThenBy(mi => mi.Id)
                .ToListAsync();
            var response = _mapper.Map<List<GetMenuItemDto>>(entities);
            return new ServiceResult(true, response);
        }

        public async Task<ServiceResult> ReorderAsync(ReorderMenuItemDto dto)
        {
            if (dto.Items.Count == 0)
                return new ServiceResult(true, "Sıralama dəyişmədi");

            var ids = dto.Items.Select(i => i.Id).ToList();
            var query = _db.MenuItems
                .Where(mi => mi.TenantId == dto.TenantId
                          && mi.MenuCategoryId == dto.MenuCategoryId
                          && ids.Contains(mi.Id));
            if (dto.BranchId.HasValue)
                query = query.Where(mi => mi.BranchId == dto.BranchId.Value);
            else
                query = query.Where(mi => mi.BranchId == null);

            var entities = await query.ToListAsync();
            var orderMap = dto.Items.ToDictionary(i => i.Id, i => i.SortOrder);

            foreach (var entity in entities)
            {
                if (orderMap.TryGetValue(entity.Id, out var newOrder))
                {
                    entity.SortOrder = newOrder;
                    _unitOfWork.Repository<MenuItem>().Update(entity);
                }
            }

            _unitOfWork.Commit();
            return new ServiceResult(true, "Sıralama uğurla yeniləndi");
        }
    }
}

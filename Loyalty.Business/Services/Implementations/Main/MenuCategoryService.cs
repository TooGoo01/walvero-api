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
    public class MenuCategoryService : IMenuCategoryService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;
        private readonly IMenuCategoryRepository _menuCategoryRepository;
        private readonly LoyaltyWalletDbContext _db;

        public MenuCategoryService(
            IUnitOfWork unitOfWork,
            IMapper mapper,
            IMenuCategoryRepository menuCategoryRepository,
            LoyaltyWalletDbContext db)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
            _menuCategoryRepository = menuCategoryRepository;
            _db = db;
        }

        // Description style: rəng #RRGGBB formatında olmalıdır, ölçü 10-40 px aralığına sıxılır
        private static void NormalizeDescriptionStyle(MenuCategory entity)
        {
            var color = entity.DescriptionColor?.Trim();
            entity.DescriptionColor =
                !string.IsNullOrWhiteSpace(color) && System.Text.RegularExpressions.Regex.IsMatch(color, "^#[0-9a-fA-F]{6}$")
                    ? color
                    : null;

            entity.DescriptionFontSize = entity.DescriptionFontSize.HasValue
                ? System.Math.Clamp(entity.DescriptionFontSize.Value, 10, 40)
                : null;
        }

        public async Task<ServiceResult> Add(AddMenuCategoryDto dto)
        {
            var entity = _mapper.Map<MenuCategory>(dto);
            entity.IsActive = true;
            NormalizeDescriptionStyle(entity);

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
            NormalizeDescriptionStyle(entity);
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

        public async Task<ServiceResult> GetByTenantAndBranchAsync(int tenantId, int? branchId, bool branchScoped)
        {
            var query = _db.MenuCategories
                .AsNoTracking()
                .Include(mc => mc.Branch)
                .Where(mc => mc.TenantId == tenantId && mc.IsActive);

            if (branchScoped)
            {
                query = query.Where(mc => mc.BranchId == branchId);
            }
            else if (branchId.HasValue)
            {
                query = query.Where(mc => mc.BranchId == branchId.Value);
            }

            var entities = await query.OrderBy(mc => mc.SortOrder).ThenBy(mc => mc.Id).ToListAsync();
            var response = _mapper.Map<List<GetMenuCategoryDto>>(entities);
            return new ServiceResult(true, response);
        }

        public async Task<ServiceResult> ReorderAsync(ReorderMenuCategoryDto dto)
        {
            if (dto.Items.Count == 0)
                return new ServiceResult(true, "Sıralama dəyişmədi");

            var ids = dto.Items.Select(i => i.Id).ToList();
            var query = _db.MenuCategories
                .Where(mc => mc.TenantId == dto.TenantId && ids.Contains(mc.Id));
            if (dto.BranchId.HasValue)
                query = query.Where(mc => mc.BranchId == dto.BranchId.Value);
            else
                query = query.Where(mc => mc.BranchId == null);

            var entities = await query.ToListAsync();
            var orderMap = dto.Items.ToDictionary(i => i.Id, i => i.SortOrder);

            foreach (var entity in entities)
            {
                if (orderMap.TryGetValue(entity.Id, out var newOrder))
                {
                    entity.SortOrder = newOrder;
                    _unitOfWork.Repository<MenuCategory>().Update(entity);
                }
            }

            _unitOfWork.Commit();
            return new ServiceResult(true, "Sıralama uğurla yeniləndi");
        }
    }
}

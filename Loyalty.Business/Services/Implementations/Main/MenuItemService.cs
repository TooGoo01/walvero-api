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
            entity.BranchId = dto.BranchId;

            // Seçilmiş kateqoriya + item filialının BranchId-i uyğun olmalıdır
            // (public menyu invariant-ı). Uyğun deyilsə item-in öz filialındakı
            // eyni adlı kateqoriyaya resolve et — yoxdursa yarat.
            var targetCat = await _db.MenuCategories
                .AsNoTracking()
                .Where(mc => mc.Id == entity.MenuCategoryId)
                .Select(mc => new { mc.Id, mc.TenantId, mc.BranchId, mc.AzName, mc.EnName, mc.RuName,
                    mc.AzDescription, mc.EnDescription, mc.RuDescription,
                    mc.DescriptionColor, mc.DescriptionFontSize, mc.SortOrder })
                .FirstOrDefaultAsync();

            if (targetCat != null)
            {
                if (entity.TenantId == null) entity.TenantId = targetCat.TenantId;
                if (targetCat.BranchId != entity.BranchId)
                {
                    var source = new MenuCategory
                    {
                        Id = targetCat.Id, TenantId = targetCat.TenantId, BranchId = targetCat.BranchId,
                        AzName = targetCat.AzName, EnName = targetCat.EnName, RuName = targetCat.RuName,
                        AzDescription = targetCat.AzDescription, EnDescription = targetCat.EnDescription,
                        RuDescription = targetCat.RuDescription, DescriptionColor = targetCat.DescriptionColor,
                        DescriptionFontSize = targetCat.DescriptionFontSize, SortOrder = targetCat.SortOrder
                    };
                    entity.MenuCategoryId = await ResolveCategoryForBranchAsync(source, entity.BranchId);
                }
            }

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

        public async Task<ServiceResult> BulkUpdateAsync(BulkUpdateMenuItemDto dto, int? lockedBranchId)
        {
            var ids = dto.Ids?.Where(i => i > 0).Distinct().ToList() ?? new List<int>();
            if (ids.Count == 0)
                return new ServiceResult(false, "Item seçilməyib");

            var query = _db.MenuItems
                .Where(mi => mi.IsActive && ids.Contains(mi.Id)
                          && (mi.TenantId == dto.TenantId || mi.MenuCategory.TenantId == dto.TenantId));
            if (lockedBranchId.HasValue)
                query = query.Where(mi => mi.BranchId == lockedBranchId.Value);

            var entities = await query.ToListAsync();
            if (entities.Count == 0)
                return new ServiceResult(false, "Seçilmiş itemlər tapılmadı");

            MenuCategory? targetCategory = null;
            if (dto.MenuCategoryId.HasValue)
            {
                targetCategory = await _db.MenuCategories
                    .FirstOrDefaultAsync(c => c.Id == dto.MenuCategoryId.Value
                                           && c.IsActive
                                           && c.TenantId == dto.TenantId);
                if (targetCategory == null)
                    return new ServiceResult(false, "Kateqoriya tapılmadı");
            }

            var branchToCat = new Dictionary<int?, int>(); // per-branch resolve cache
            foreach (var e in entities)
            {
                // Yalnız dolu sahələr — qiymət/endirim QƏSDƏN toxunulmur (filial-spesifik)
                if (!string.IsNullOrWhiteSpace(dto.AzName)) e.AzName = dto.AzName.Trim();
                if (!string.IsNullOrWhiteSpace(dto.EnName)) e.EnName = dto.EnName.Trim();
                if (!string.IsNullOrWhiteSpace(dto.RuName)) e.RuName = dto.RuName.Trim();
                if (!string.IsNullOrWhiteSpace(dto.AzDescription)) e.AzDescription = dto.AzDescription.Trim();
                if (!string.IsNullOrWhiteSpace(dto.EnDescription)) e.EnDescription = dto.EnDescription.Trim();
                if (!string.IsNullOrWhiteSpace(dto.RuDescription)) e.RuDescription = dto.RuDescription.Trim();
                if (!string.IsNullOrWhiteSpace(dto.PrepTimeMinutes)) e.PrepTimeMinutes = dto.PrepTimeMinutes.Trim();
                // Qiymət yalnız user bilərəkdən dolu göndərəndə dəyişir
                if (dto.Price.HasValue && dto.Price.Value >= 0) e.Price = dto.Price.Value;
                if (dto.DiscountPrice.HasValue && dto.DiscountPrice.Value >= 0) e.DiscountPrice = dto.DiscountPrice.Value;
                if (targetCategory != null)
                {
                    // İnvariant: item HƏMİŞƏ öz filialındakı (null = Ümumi) eyni adlı
                    // kateqoriyaya köçür — yoxdursa yarat. Filial dəyişmir. Beləliklə
                    // item.BranchId == kateqoriya.BranchId qalır (public menyu gizlətməz).
                    if (!branchToCat.TryGetValue(e.BranchId, out var catId))
                    {
                        catId = await ResolveCategoryForBranchAsync(targetCategory, e.BranchId);
                        branchToCat[e.BranchId] = catId;
                    }
                    e.MenuCategoryId = catId;
                }
                _unitOfWork.Repository<MenuItem>().Update(e);
            }
            _unitOfWork.Commit();

            return new ServiceResult(true, entities.Select(x => x.Id).ToList(),
                $"{entities.Count} item yeniləndi");
        }

        /// <summary>
        /// Verilən filialda (null = Ümumi/tenant-wide) source ilə eyni AzName-li aktiv kateqoriyanı tapır;
        /// yoxdursa source-un kopyasını həmin filialda yaradır. source artıq həmin filialdadırsa olduğu kimi qaytarır.
        /// </summary>
        private async Task<int> ResolveCategoryForBranchAsync(MenuCategory source, int? branchId)
        {
            if (source.BranchId == branchId)
                return source.Id;

            // Ad-uyğunluğu trim ilə: "Sağlam Qida " (boşluqlu) ≡ "Sağlam Qida" —
            // trailing-space dublikat kateqoriya yaranmasın.
            var srcName = (source.AzName ?? string.Empty).Trim();
            var existing = await _db.MenuCategories.AsNoTracking()
                .Where(mc => mc.TenantId == source.TenantId
                          && mc.BranchId == branchId
                          && mc.IsActive
                          && mc.AzName.Trim() == srcName)
                .Select(mc => (int?)mc.Id)
                .FirstOrDefaultAsync();
            if (existing != null)
                return existing.Value;

            var copy = new MenuCategory
            {
                AzName = srcName,
                EnName = source.EnName,
                RuName = source.RuName,
                AzDescription = source.AzDescription,
                EnDescription = source.EnDescription,
                RuDescription = source.RuDescription,
                DescriptionColor = source.DescriptionColor,
                DescriptionFontSize = source.DescriptionFontSize,
                SortOrder = source.SortOrder,
                TenantId = source.TenantId,
                BranchId = branchId,
                IsActive = true,
                RegDate = DateTime.UtcNow
            };
            _db.MenuCategories.Add(copy);
            await _db.SaveChangesAsync();
            return copy.Id;
        }
    }
}

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

namespace Loyalty.Business.Services.Implementations.Main
{
    public class TenantLinkService : ITenantLinkService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;
        private readonly ITenantLinkRepository _tenantLinkRepository;
        private readonly LoyaltyWalletDbContext _db;

        public TenantLinkService(
            IUnitOfWork unitOfWork,
            IMapper mapper,
            ITenantLinkRepository tenantLinkRepository,
            LoyaltyWalletDbContext db)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
            _tenantLinkRepository = tenantLinkRepository;
            _db = db;
        }

        public async Task<ServiceResult> Add(AddTenantLinkDto dto)
        {
            var entity = _mapper.Map<TenantLink>(dto);
            entity.IsActive = true;

            await _unitOfWork.Repository<TenantLink>().AddAsync(entity);
            _unitOfWork.Commit();

            var response = _mapper.Map<GetTenantLinkDto>(entity);
            return new ServiceResult(true, response, "Link uğurla əlavə edildi");
        }

        public async Task<ServiceResult> Update(UpdateTenantLinkDto dto)
        {
            var entity = await _unitOfWork.Repository<TenantLink>().GetByIdAsync(dto.Id);
            if (entity == null)
                return new ServiceResult(false, "Link tapılmadı");

            _mapper.Map(dto, entity);
            _unitOfWork.Repository<TenantLink>().Update(entity);
            _unitOfWork.Commit();

            var response = _mapper.Map<GetTenantLinkDto>(entity);
            return new ServiceResult(true, response, "Link uğurla yeniləndi");
        }

        public async Task<ServiceResult> Delete(int id)
        {
            var entity = await _unitOfWork.Repository<TenantLink>().GetByIdAsync(id);
            if (entity == null)
                return new ServiceResult(false, "Link tapılmadı");

            entity.IsActive = false;
            _unitOfWork.Repository<TenantLink>().Update(entity);
            _unitOfWork.Commit();

            return new ServiceResult(true, "Link uğurla silindi");
        }

        public async Task<ServiceResult> GetById(int id)
        {
            var entity = await _unitOfWork.Repository<TenantLink>().GetByIdAsync(id);
            if (entity == null)
                return new ServiceResult(false, "Link tapılmadı");

            var response = _mapper.Map<GetTenantLinkDto>(entity);
            return new ServiceResult(true, response);
        }

        public async Task<ServiceResult> GetByTenantId(int tenantId)
        {
            var entities = await _tenantLinkRepository.GetByTenantIdOrderedAsync(tenantId);
            var response = _mapper.Map<List<GetTenantLinkDto>>(entities);
            return new ServiceResult(true, response);
        }

        public async Task<ServiceResult> GetByTenantAndBranchAsync(int tenantId, int? branchId, bool branchScoped)
        {
            var query = _db.TenantLinks
                .AsNoTracking()
                .Include(tl => tl.Branch)
                .Where(tl => tl.TenantId == tenantId && tl.IsActive);

            if (branchScoped)
            {
                query = query.Where(tl => tl.BranchId == branchId);
            }
            else if (branchId.HasValue)
            {
                query = query.Where(tl => tl.BranchId == branchId.Value);
            }

            var entities = await query.OrderBy(tl => tl.SortOrder).ToListAsync();
            var response = _mapper.Map<List<GetTenantLinkDto>>(entities);
            return new ServiceResult(true, response);
        }

        public async Task<ServiceResult> Reorder(ReorderTenantLinksDto dto)
        {
            if (dto.Items.Count == 0)
                return new ServiceResult(true, "Sıralama dəyişmədi");

            var ids = dto.Items.Select(i => i.Id).ToList();
            var entities = (await _tenantLinkRepository.GetByTenantIdOrderedAsync(dto.TenantId))
                .Where(tl => ids.Contains(tl.Id))
                .ToList();

            var orderMap = dto.Items.ToDictionary(i => i.Id, i => i.SortOrder);

            foreach (var entity in entities)
            {
                if (orderMap.TryGetValue(entity.Id, out var newOrder))
                {
                    entity.SortOrder = newOrder;
                    _unitOfWork.Repository<TenantLink>().Update(entity);
                }
            }

            _unitOfWork.Commit();
            return new ServiceResult(true, "Sıralama uğurla yeniləndi");
        }
    }
}

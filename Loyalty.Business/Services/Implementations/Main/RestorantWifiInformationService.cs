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
    public class RestorantWifiInformationService : IRestorantWifiInformationService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;
        private readonly IRestorantWifiInformationRepository _restorantWifiInformationRepository;
        private readonly LoyaltyWalletDbContext _db;

        public RestorantWifiInformationService(
            IUnitOfWork unitOfWork,
            IMapper mapper,
            IRestorantWifiInformationRepository restorantWifiInformationRepository,
            LoyaltyWalletDbContext db)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
            _restorantWifiInformationRepository = restorantWifiInformationRepository;
            _db = db;
        }

        public async Task<ServiceResult> Add(AddRestorantWifiInformationDto dto)
        {
            var entity = _mapper.Map<RestorantWifiInformation>(dto);
            entity.IsActive = true;
            
            await _unitOfWork.Repository<RestorantWifiInformation>().AddAsync(entity);
            _unitOfWork.Commit();

            var response = _mapper.Map<GetRestorantWifiInformationDto>(entity);
            return new ServiceResult(true, response, "Restoran WiFi məlumatı uğurla əlavə edildi");
        }

        public async Task<ServiceResult> Update(UpdateRestorantWifiInformationDto dto)
        {
            var entity = await _unitOfWork.Repository<RestorantWifiInformation>().GetByIdAsync(dto.Id);
            if (entity == null)
                return new ServiceResult(false, "Restoran WiFi məlumatı tapılmadı");

            _mapper.Map(dto, entity);
            _unitOfWork.Repository<RestorantWifiInformation>().Update(entity);
            _unitOfWork.Commit();

            var response = _mapper.Map<GetRestorantWifiInformationDto>(entity);
            return new ServiceResult(true, response, "Restoran WiFi məlumatı uğurla yeniləndi");
        }

        public async Task<ServiceResult> Delete(int id)
        {
            var entity = await _unitOfWork.Repository<RestorantWifiInformation>().GetByIdAsync(id);
            if (entity == null)
                return new ServiceResult(false, "Restoran WiFi məlumatı tapılmadı");

            entity.IsActive = false;
            _unitOfWork.Repository<RestorantWifiInformation>().Update(entity);
            _unitOfWork.Commit();

            return new ServiceResult(true, "Restoran WiFi məlumatı uğurla silindi");
        }

        public async Task<ServiceResult> GetById(int id)
        {
            var entity = await _unitOfWork.Repository<RestorantWifiInformation>().GetByIdAsync(id);
            if (entity == null)
                return new ServiceResult(false, "Restoran WiFi məlumatı tapılmadı");

            var response = _mapper.Map<GetRestorantWifiInformationDto>(entity);
            return new ServiceResult(true, response);
        }

        public async Task<ServiceResult> GetByIdWithTenant(int id)
        {
            var entity = await _restorantWifiInformationRepository.GetByIdWithTenantAsync(id);
            if (entity == null)
                return new ServiceResult(false, "Restoran WiFi məlumatı tapılmadı");

            var response = _mapper.Map<GetRestorantWifiInformationDto>(entity);
            return new ServiceResult(true, response);
        }

        public async Task<ServiceResult> GetByTenantId(int tenantId)
        {
            var entities = await _restorantWifiInformationRepository.GetByTenantIdAsync(tenantId);
            var response = _mapper.Map<List<GetRestorantWifiInformationDto>>(entities);
            return new ServiceResult(true, response);
        }

        public async Task<ServiceResult> GetByTenantAndBranchAsync(int tenantId, int? branchId, bool branchScoped)
        {
            var query = _db.RestorantWifiInformations
                .AsNoTracking()
                .Include(w => w.Branch)
                .Where(w => w.TenantId == tenantId && w.IsActive);

            if (branchScoped)
                query = query.Where(w => w.BranchId == branchId);
            else if (branchId.HasValue)
                query = query.Where(w => w.BranchId == branchId.Value);

            var entities = await query.OrderBy(w => w.Id).ToListAsync();
            var response = _mapper.Map<List<GetRestorantWifiInformationDto>>(entities);
            return new ServiceResult(true, response);
        }

        public async Task<ServiceResult> GetAll()
        {
            var entities = await _unitOfWork.Repository<RestorantWifiInformation>().GetAllAsync();
            var response = _mapper.Map<List<GetRestorantWifiInformationDto>>(entities);
            return new ServiceResult(true, response);
        }

        public async Task<ServiceResult> GetAllWithTenant()
        {
            var entities = await _restorantWifiInformationRepository.GetAllWithTenantAsync();
            var response = _mapper.Map<List<GetRestorantWifiInformationDto>>(entities);
            return new ServiceResult(true, response);
        }
    }
}

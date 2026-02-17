using AutoMapper;
using Loyalty.Business.Dtos.Get.Main;
using Loyalty.Business.Dtos.Post.Main;
using Loyalty.Business.Results;
using Loyalty.Business.Services.Abstractions.Main;
using Loyalty.DataAccess.Entities.Main;
using Loyalty.DataAccess.Repositories.Abstractions.Main;
using Loyalty.DataAccess.UnitOfWorks;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Loyalty.Business.Services.Implementations.Main
{
    public class RestorantWifiInformationService : IRestorantWifiInformationService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;
        private readonly IRestorantWifiInformationRepository _restorantWifiInformationRepository;

        public RestorantWifiInformationService(
            IUnitOfWork unitOfWork,
            IMapper mapper,
            IRestorantWifiInformationRepository restorantWifiInformationRepository)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
            _restorantWifiInformationRepository = restorantWifiInformationRepository;
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

using AutoMapper;
using Loyalty.Business.Dtos.Get.Main;
using Loyalty.Business.Dtos.Post.Main;
using Loyalty.Business.Results;
using Loyalty.Business.Services.Abstractions.Main;
using Loyalty.DataAccess.Entities.Main;
using Loyalty.DataAccess.Repositories.Abstractions.Main;
using Loyalty.DataAccess.UnitOfWorks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Loyalty.Business.Services.Implementations.Main
{
    public class PartnerService : IPartnerService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;
        private readonly IPartnerRepository _partnerRepository;

        public PartnerService(IPartnerRepository partnerRepository, IMapper mapper, IUnitOfWork unitOfWork)
        {
            _partnerRepository = partnerRepository;
            _mapper = mapper;
            _unitOfWork = unitOfWork;
        }

        public async Task<ServiceResult> Add(AddPartnerDto Dto) { 
        
            var request = _mapper.Map<Partner>(Dto);
            request.IsActive = true;
            await _unitOfWork.Repository<Partner>().AddAsync(request);
            _unitOfWork.Commit();
            var response = _mapper.Map<GetPartnerDto>(request);
            return new ServiceResult(true, response.Id);
        }

        public async Task<ServiceResult> Delete(int id)
        {
            var about = await _partnerRepository.GetById(id);
            if (about != null)
            {
                about.IsActive = false;
                _unitOfWork.Repository<Partner>().Update(about);
                _unitOfWork.Commit();
                return new ServiceResult(true);
            }
            return new ServiceResult(false);
        }

        public async Task<ServiceResult> Get(int id)
        {
            try
            {
                var result = await _partnerRepository.GetById(id);
                if (result == null)
                    return new ServiceResult(false, "data not found");

                var response = _mapper.Map<GetPartnerDto>(result);
                return new ServiceResult(true, response);
            }
            catch (Exception ex)
            {
                return new ServiceResult(false, ex.Message);
            }
        }

        public async Task<ServiceResult> GetAll()
        {
            var partners = await _partnerRepository.GetAll();
            if (partners != null)
            {
                var response = _mapper.Map<ICollection<GetPartnerDto>>(partners);
                return new ServiceResult(true, response);
            }

            return new ServiceResult(false);
        }


        public Task<ServiceResult> GetLast()
        {
            throw new NotImplementedException();
        }

        public Task<ServiceResult> Update(AddPartnerDto dto, int id)
        {
            throw new NotImplementedException();
        }
    }
}

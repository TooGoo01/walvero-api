using AutoMapper;
using Loyalty.Business.Dtos.Post.Main;
using Loyalty.Business.Results;
using Loyalty.Business.Services.Abstractions.Main;
using Loyalty.Business.Services.Abstractions.Storage;
using Loyalty.DataAccess.Entities.Main;
using Loyalty.DataAccess.UnitOfWorks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Loyalty.Business.Services.Implementations.Main
{
    public class PartnerFileService : IPartnerFileService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IStorage _storage;
        private readonly IMapper _mapper;
        public PartnerFileService(IUnitOfWork unitOfWork, IStorage storage, IMapper mapper)
        {
            _unitOfWork = unitOfWork;
            _storage = storage;
            _mapper = mapper;
        }

        public async Task<ServiceResult> AddRangeAsync(AddPartnerDto partnerDto, int partnerId)
        {
            List<(string fileName, string pathOrContainerName)> result = await _storage.UploadAsync("photo-about", partnerDto.Files);
            var about = _unitOfWork.Repository<Partner>().Get(x => x.Id == partnerId);
            _unitOfWork.Repository<PartnerFile>().AddRange(result.Select(x => new PartnerFile
            {
                FileName = x.fileName,
                Path = x.pathOrContainerName,
                Partner = about
            }));
            _unitOfWork.Commit();
            return new ServiceResult(true);
        }

        public async Task<ServiceResult> UpdateRangeAsync(AddPartnerDto partnerDto, int partnerId)
        {
            List<(string fileName, string pathOrContainerName)> result = await _storage.UploadAsync("photo-about", partnerDto.Files);
            var about = _unitOfWork.Repository<Partner>().Get(x => x.Id == partnerId);
            var entity = await _unitOfWork.Repository<PartnerFile>().GetAllAsync(x => x.Partner.Id == partnerId);
            _unitOfWork.Repository<PartnerFile>().DeleteRange(entity);
            _unitOfWork.Repository<PartnerFile>().UpdateRange(result.Select(x => new PartnerFile
            {
                FileName = x.fileName,
                Path = x.pathOrContainerName,
                Partner = about
            }));
            _unitOfWork.Commit();
            return new ServiceResult(true);
        }
    }
}

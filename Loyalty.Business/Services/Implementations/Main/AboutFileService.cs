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
    public class AboutFileService:IAboutFileService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IStorage _storage;
        private readonly IMapper _mapper;
        public AboutFileService(IUnitOfWork unitOfWork, IStorage storage, IMapper mapper)
        {
            _unitOfWork = unitOfWork;
            _storage = storage;
            _mapper = mapper;
        }

        public async Task<ServiceResult> AddRangeAsync(AddAboutDto aboutDto, int aboutId)
        {
            List<(string fileName, string pathOrContainerName)> result = await _storage.UploadAsync("photo-about", aboutDto.Files);
            var about = _unitOfWork.Repository<About>().Get(x => x.Id == aboutId);
            _unitOfWork.Repository<AboutFile>().AddRange(result.Select(x => new AboutFile
            {
                FileName = x.fileName,
                Path = x.pathOrContainerName,
                About = about
            }));
            _unitOfWork.Commit();
            return new ServiceResult(true);
        }

        public async Task<ServiceResult> UpdateRangeAsync(AddAboutDto aboutDto, int aboutId)
        {
            List<(string fileName, string pathOrContainerName)> result = await _storage.UploadAsync("photo-about", aboutDto.Files);
            var about = _unitOfWork.Repository<About>().Get(x => x.Id == aboutId);
            var entity = await _unitOfWork.Repository<AboutFile>().GetAllAsync(x => x.About.Id == aboutId);
            _unitOfWork.Repository<AboutFile>().DeleteRange(entity);
            _unitOfWork.Repository<AboutFile>().UpdateRange(result.Select(x => new AboutFile
            {
                FileName = x.fileName,
                Path = x.pathOrContainerName,
                About = about
            }));
            _unitOfWork.Commit();
            return new ServiceResult(true);
        }
    }
}

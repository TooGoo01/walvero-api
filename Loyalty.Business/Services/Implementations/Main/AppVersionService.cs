using AutoMapper;
using Loyalty.Business.Dtos.Get.Main;
using Loyalty.Business.Dtos.Post.Main;
using Loyalty.Business.Results;
using Loyalty.Business.Services.Abstractions.Main;
using Loyalty.DataAccess.Entities.Main;
using Loyalty.DataAccess.Repositories.Abstractions.Main;
using Loyalty.DataAccess.UnitOfWorks;



namespace Loyalty.Business.Services.Implementations.Main
{
    public class AppVersionService : IAppVersionService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;
        private readonly IAppVersionRepository _appVersionRepository;

        public AppVersionService(IAppVersionRepository appVersionRepository, IMapper mapper, IUnitOfWork unitOfWork)
        {
            _appVersionRepository = appVersionRepository;
            _mapper = mapper;
            _unitOfWork = unitOfWork;
        }

        public async Task<ServiceResult> Active(int id)
        {
            var about = await _appVersionRepository.GetById(id);
            if (about != null)
            {
                about.IsActive = true;
                _unitOfWork.Repository<AppVersionConfig>().Update(about);
                _unitOfWork.Commit();
                return new ServiceResult(true);
            }
            return new ServiceResult(false);
        }

        public async Task<ServiceResult> Add(AddAppVersion Dto)
        {
            var request = _mapper.Map<AppVersionConfig>(Dto);
            request.IsActive = true;
            await _unitOfWork.Repository<AppVersionConfig>().AddAsync(request);
            _unitOfWork.Commit();
            var response = _mapper.Map<GetAppVersionDto>(request);
            return new ServiceResult(true, response.Id);
        }

        public async Task<ServiceResult> Check(string plaform, string currentVersion)
        {
            var respose = await _appVersionRepository.GetActiveByPlatformAsync(plaform);
            if (respose == null)
            {
                return new ServiceResult(true, "404");
            }
            if (!currentVersion.Equals(respose.LatestVersion)) {
                return new ServiceResult(true,respose, "200");
            }
            if (currentVersion.Equals(respose.LatestVersion))
            {
                return new ServiceResult(true, "302");
            }

            var response = _mapper.Map<List<GetAppVersionDto>>(respose);
            return new ServiceResult(true, response);
        }

        public async Task<ServiceResult> Deactive(int id)
        {
            var about = await _appVersionRepository.GetById(id);
            if (about != null)
            {
                about.IsActive = false;
                _unitOfWork.Repository<AppVersionConfig>().Update(about);
                _unitOfWork.Commit();
                return new ServiceResult(true);
            }
            return new ServiceResult(false);
        }

        public Task<ServiceResult> Delete(int id)
        {
            throw new NotImplementedException();
        }

        public async Task<ServiceResult> Get(int id)
        {
            try
            {
                var result = await _appVersionRepository.GetById(id);
                if (result == null)
                    return new ServiceResult(false, "data not found");

                var response = _mapper.Map<GetAppVersionDto>(result);
                return new ServiceResult(true, response);
            }
            catch (Exception ex)
            {
                return new ServiceResult(false, ex.Message);
            }
        }

        public async Task<ServiceResult> GetActive()
        {
            var partners = await _appVersionRepository.GetActiveList();
            if (partners != null)
            {
                var response = _mapper.Map<ICollection<GetAppVersionDto>>(partners);
                return new ServiceResult(true, response);
            }

            return new ServiceResult(false);
        }

        public async Task<ServiceResult> GetAll()
        {
            var partners = await _appVersionRepository.GetAllList();
            if (partners != null)
            {
                var response = _mapper.Map<ICollection<GetAppVersionDto>>(partners);
                return new ServiceResult(true, response);
            }

            return new ServiceResult(false);
        }
        public string Platform { get; set; } = default!;

        public bool IsMandatory { get; set; }

        public string LatestVersion { get; set; } = default!;
        public string? Description { get; set; }
        public string? MessageTitle { get; set; }
        public string? Message { get; set; }
        public string? StoreUrl { get; set; }

        public async Task<ServiceResult> Update(AddAppVersion dto, int id)
        {
            var category = await _appVersionRepository.GetById(id);
            if (category != null)
            {
                if (dto.LatestVersion != null)
                {
                    category.LatestVersion = dto.LatestVersion;
                }
                if (dto.Description != null)
                {
                    category.Description = dto.Description;
                }
                if (dto.MessageTitle != null)
                {
                    category.MessageTitle = dto.MessageTitle;
                }
                if (dto.Message != null)
                {
                    category.Message = dto.Message;
                }
                if (dto.StoreUrl != null)
                {
                    category.StoreUrl = dto.StoreUrl;
                }
                if (dto.Platform != null)
                {
                    category.Platform = dto.Platform;
                }

               

               
                _unitOfWork.Repository<AppVersionConfig>().Update(category);
                _unitOfWork.Commit();
                var response = _mapper.Map<GetAppVersionDto>(category);
                return new ServiceResult(true, response.Id);
            }
            return new ServiceResult(false);
        }
    }
}

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
	public class AboutService : IAboutService
	{
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;
        private readonly IAboutRepository _aboutRepository;

        public AboutService(IUnitOfWork unitOfWork, IMapper mapper, IAboutRepository aboutRepository)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
            _aboutRepository = aboutRepository;
            
        }

        public async Task<ServiceResult> AddAbout(AddAboutDto aboutDto)
        {
            var request = _mapper.Map<About>(aboutDto);
            request.IsActive = true;
            await _unitOfWork.Repository<About>().AddAsync(request);
            _unitOfWork.Commit();
            var response = _mapper.Map<GetAboutDto>(request);
            return new ServiceResult(true, response.Id);
        }

        public async Task<ServiceResult> DeleteAbout(int id)
        {
            var about = await _aboutRepository.GetAbout(id);
            if (about != null)
            {
                about.IsActive = false;
                _unitOfWork.Repository<About>().Update(about);
                _unitOfWork.Commit();
                return new ServiceResult(true);
            }
            return new ServiceResult(false);
        }

        public async Task<ServiceResult> GetAbout(int id)
        {
            try
            {
                var result = await _aboutRepository.GetAbout(id);
                if (result == null)
                    return new ServiceResult(false, "data not found");

                var response = _mapper.Map<GetAboutDto>(result);
                return new ServiceResult(true, response);
            }
            catch (Exception ex)
            {
                return new ServiceResult(false, ex.Message);
            }
        }

        public async Task<ServiceResult> GetAbout()
        {
            try
            {
                var result = await _aboutRepository.GetAbout();
                if (result == null)
                    return new ServiceResult(false, "data not found");

                var response = _mapper.Map<GetAboutDto>(result);
                return new ServiceResult(true, response);
            }
            catch (Exception ex)
            {
                return new ServiceResult(false, ex.Message);
            }
        }

        public async Task<ServiceResult> GetAbouts()
        {
            var partners = await _aboutRepository.GetAllAbouts();
            if (partners != null)
            {
                var response = _mapper.Map<ICollection<GetAboutDto>>(partners);
                return new ServiceResult(true, response);
            }

            return new ServiceResult(false);
        }

        public async Task<ServiceResult> GetActiveAbouts()
        {
            var partner = await _aboutRepository.GetActiveAbouts();
            if (partner != null)
            {
                var response = _mapper.Map<ICollection<GetAboutDto>>(partner);
                return new ServiceResult(true, response);
            }

            return new ServiceResult(false);
        }

        public async Task<ServiceResult> GetLastAbout()
        {
            try
            {
                var result = await _aboutRepository.GetLastAbout();
                if (result == null)
                    return new ServiceResult(false, "data not found");

                var response = _mapper.Map<GetAboutDto>(result);
                return new ServiceResult(true, response);
            }
            catch (Exception ex)
            {
                return new ServiceResult(false, ex.Message);
            }
        }

        public async Task<ServiceResult> UpdateAbout(AddAboutDto aboutDto, int id)
        {
            var about = await _aboutRepository.GetAbout(id);
            if (about != null)
            {
                //if (!aboutDto.AzTitle.IsNullOrEmpty())
                //{
                //    about.AzTitle = aboutDto.AzTitle;
                //}

                //if (!aboutDto.AzDescription.IsNullOrEmpty())
                //{
                //    about.AzDescription = aboutDto.AzDescription;
                //}
                //if (!aboutDto.EnTitle.IsNullOrEmpty())
                //{
                //    about.EnTitle = aboutDto.EnTitle;
                //}

                //if (!aboutDto.EnDescription.IsNullOrEmpty())
                //{
                //    about.EnDescription = aboutDto.EnDescription;
                //}
                //if (!aboutDto.RuTitle.IsNullOrEmpty())
                //{
                //    about.RuTitle = aboutDto.RuTitle;
                //}

                //if (!aboutDto.RuDescription.IsNullOrEmpty())
                //{
                //    about.RuDescription = aboutDto.RuDescription;
                //}
                //if (!aboutDto.Email.IsNullOrEmpty())
                //{
                //    about.Email = aboutDto.Email;
                //}
                //if (!aboutDto.Phone.IsNullOrEmpty())
                //{
                //    about.Phone = aboutDto.Phone;
                //} 
                //if (!aboutDto.Instagram.IsNullOrEmpty())
                //{
                //    about.Instagram = aboutDto.Instagram;
                //}
                //if (!aboutDto.Twitter.IsNullOrEmpty())
                //{
                //    about.Twitter = aboutDto.Twitter;
                //}

                _unitOfWork.Repository<About>().Update(about);
                _unitOfWork.Commit();
                var response = _mapper.Map<GetAboutDto>(about);
                return new ServiceResult(true, response.Id);
            }
            return new ServiceResult(false);
        }
    }
}

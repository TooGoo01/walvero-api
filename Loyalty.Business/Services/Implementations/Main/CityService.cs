using AutoMapper;
using Loyalty.Business.Dtos.Get.Main;
using Loyalty.Business.Dtos.Post.Main;
using Loyalty.Business.Results;
using Loyalty.Business.Services.Abstractions.Main;
using Loyalty.DataAccess.Entities.Main;
using Loyalty.DataAccess.UnitOfWorks;
using Microsoft.EntityFrameworkCore;

namespace Loyalty.Business.Services.Implementations.Main
{
    public class CityService : ICityService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;

        public CityService(IMapper mapper, IUnitOfWork unitOfWork)
        {
            _mapper = mapper;
            _unitOfWork = unitOfWork;
        }

        public async Task<ServiceResult> AddCity(AddCityDto city)
        {
            var request = _mapper.Map<City>(city);
            await _unitOfWork.Repository<City>().AddAsync(request);
            _unitOfWork.Commit();
            var response = _mapper.Map<GetCityDto>(request);
            return new ServiceResult(true, response.Id);
        }

        public async Task<ServiceResult> GetAllCities()
        {
            var cities = _unitOfWork.Repository<City>().Query()
                .Include(c => c.Country)
                .ToList();
            if (cities != null)
            {
                var response = cities.Select(c => new GetCityDto
                {
                    Id = c.Id,
                    AzName = c.AzName,
                    EnName = c.EnName,
                    RuName = c.RuName,
                    countryId = c.countryId,
                    CountryAzName = c.Country?.AzName,
                    CountryEnName = c.Country?.EnName,
                    CountryRuName = c.Country?.RuName
                }).ToList();
                return new ServiceResult(true, response);
            }
            return new ServiceResult(false);
        }

        public async Task<ServiceResult> GetCitiesByCountry(int countryId)
        {
            var cities = _unitOfWork.Repository<City>().Query()
                .Where(c => c.countryId == countryId && c.IsActive)
                .Include(c => c.Country)
                .OrderBy(c => c.AzName)
                .ToList();
            if (cities != null)
            {
                var response = cities.Select(c => new GetCityDto
                {
                    Id = c.Id,
                    AzName = c.AzName,
                    EnName = c.EnName,
                    RuName = c.RuName,
                    countryId = c.countryId,
                    CountryAzName = c.Country?.AzName,
                    CountryEnName = c.Country?.EnName,
                    CountryRuName = c.Country?.RuName
                }).ToList();
                return new ServiceResult(true, response);
            }
            return new ServiceResult(false);
        }

        public async Task<ServiceResult> GetCity(int id)
        {
            try
            {
                var result = _unitOfWork.Repository<City>().Query()
                    .Include(c => c.Country)
                    .FirstOrDefault(c => c.Id == id);
                if (result == null)
                    return new ServiceResult(false, "data not found");

                var response = new GetCityDto
                {
                    Id = result.Id,
                    AzName = result.AzName,
                    EnName = result.EnName,
                    RuName = result.RuName,
                    countryId = result.countryId,
                    CountryAzName = result.Country?.AzName,
                    CountryEnName = result.Country?.EnName,
                    CountryRuName = result.Country?.RuName
                };
                return new ServiceResult(true, response);
            }
            catch (Exception ex)
            {
                return new ServiceResult(false, ex.Message);
            }
        }

        public async Task<ServiceResult> UpdateCity(AddCityDto cityDto, int id)
        {
            var city = await _unitOfWork.Repository<City>().GetByIdAsync(id);
            if (city != null)
            {
                if (!string.IsNullOrWhiteSpace(cityDto.AzName))
                {
                    city.AzName = cityDto.AzName.Trim();
                }
                if (!string.IsNullOrWhiteSpace(cityDto.EnName))
                {
                    city.EnName = cityDto.EnName.Trim();
                }
                if (!string.IsNullOrWhiteSpace(cityDto.RuName))
                {
                    city.RuName = cityDto.RuName.Trim();
                }
                if (cityDto.countryId > 0)
                {
                    city.countryId = cityDto.countryId;
                }

                _unitOfWork.Repository<City>().Update(city);
                _unitOfWork.Commit();
                var response = _mapper.Map<GetCityDto>(city);
                return new ServiceResult(true, response.Id);
            }
            return new ServiceResult(false);
        }

        public async Task<ServiceResult> DeleteCity(int id)
        {
            var city = await _unitOfWork.Repository<City>().GetByIdAsync(id);
            if (city != null)
            {
                _unitOfWork.Repository<City>().Delete(city);
                _unitOfWork.Commit();
                return new ServiceResult(true);
            }
            return new ServiceResult(false);
        }
    }
}

using AutoMapper;
using Loyalty.Business.Dtos.Get.Main;
using Loyalty.Business.Dtos.Post.Main;
using Loyalty.Business.Results;
using Loyalty.Business.Services.Abstractions.Main;
using Loyalty.DataAccess.Entities.Main;
using Loyalty.DataAccess.UnitOfWorks;


namespace Loyalty.Business.Services.Implementations.Main
{
    public class CountryService : ICountryService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;

        public CountryService(IMapper mapper, IUnitOfWork unitOfWork)
        {
            _mapper = mapper;
            _unitOfWork = unitOfWork;
        }

        public async Task<ServiceResult> AddCountry(AddCountryDto country)
        {
            var request = _mapper.Map<Country>(country);
            await _unitOfWork.Repository<Country>().AddAsync(request);
            _unitOfWork.Commit();
            var response = _mapper.Map<GetCountryDto>(request);
            return new ServiceResult(true, response.Id);
        }

        public async Task<ServiceResult> GetAllCountries()
        {
            var countries = await _unitOfWork.Repository<Country>().GetAllAsync();
            if (countries != null)
            {
                var response = _mapper.Map<ICollection<GetCountryDto>>(countries);
                return new ServiceResult(true, response);
            }
            return new ServiceResult(false);
        }

        public async Task<ServiceResult> GetAllCountriesWithCities()
        {
            try
            {
                // Get all active countries ordered by AzName
                var countries = _unitOfWork.Repository<Country>().Query()
                    .Where(c => c.IsActive)
                    .OrderBy(c => c.AzName)
                    .ToList();

                if (countries == null || !countries.Any())
                    return new ServiceResult(false, "No countries found");

                // Get all active cities
                var cities = _unitOfWork.Repository<City>().Query()
                    .Where(c => c.IsActive)
                    .OrderBy(c => c.AzName)
                    .ToList();

                // Map countries with their cities
                var response = countries.Select(country => new GetCountryWithCitiesDto
                {
                    Id = country.Id,
                    AzName = country.AzName,
                    EnName = country.EnName,
                    RuName = country.RuName,
                    Cities = cities
                        .Where(c => c.countryId == country.Id)
                        .Select(c => new GetCityDto
                        {
                            Id = c.Id,
                            AzName = c.AzName,
                            EnName = c.EnName,
                            RuName = c.RuName,
                            countryId = c.countryId
                        })
                        .ToList()
                }).ToList();

                return new ServiceResult(true, response);
            }
            catch (Exception ex)
            {
                return new ServiceResult(false, ex.Message);
            }
        }

        public async Task<ServiceResult> GetCountry(int id)
        {
            try
            {
                var result = await _unitOfWork.Repository<Country>().GetByIdAsync(id);
                if (result == null)
                    return new ServiceResult(false, "data not found");

                var response = _mapper.Map<GetCountryDto>(result);
                return new ServiceResult(true, response);
            }
            catch (Exception ex)
            {
                return new ServiceResult(false, ex.Message);
            }
        }

        public async Task<ServiceResult> GetCountryWithCities(int id)
        {
            try
            {
                var country = _unitOfWork.Repository<Country>().Query()
                    .Where(c => c.Id == id && c.IsActive)
                    .FirstOrDefault();

                if (country == null)
                    return new ServiceResult(false, "Country not found");

                var cities = _unitOfWork.Repository<City>().Query()
                    .Where(c => c.countryId == id && c.IsActive)
                    .OrderBy(c => c.AzName)
                    .ToList();

                var response = new GetCountryWithCitiesDto
                {
                    Id = country.Id,
                    AzName = country.AzName,
                    EnName = country.EnName,
                    RuName = country.RuName,
                    Cities = cities.Select(c => new GetCityDto
                    {
                        Id = c.Id,
                        AzName = c.AzName,
                        EnName = c.EnName,
                        RuName = c.RuName,
                        countryId = c.countryId
                    }).ToList()
                };

                return new ServiceResult(true, response);
            }
            catch (Exception ex)
            {
                return new ServiceResult(false, ex.Message);
            }
        }

        public async Task<ServiceResult> UpdateCountry(AddCountryDto countryDto, int id)
        {
            var country = await _unitOfWork.Repository<Country>().GetByIdAsync(id);
            if (country != null)
            {
                if (!string.IsNullOrWhiteSpace(countryDto.AzName))
                {
                    country.AzName = countryDto.AzName.Trim();
                }
                if (!string.IsNullOrWhiteSpace(countryDto.EnName))
                {
                    country.EnName = countryDto.EnName.Trim();
                }
                if (!string.IsNullOrWhiteSpace(countryDto.RuName))
                {
                    country.RuName = countryDto.RuName.Trim();
                }

                _unitOfWork.Repository<Country>().Update(country);
                _unitOfWork.Commit();
                var response = _mapper.Map<GetCountryDto>(country);
                return new ServiceResult(true, response.Id);
            }
            return new ServiceResult(false);
        }

        public async Task<ServiceResult> DeleteCountry(int id)
        {
            var country = await _unitOfWork.Repository<Country>().GetByIdAsync(id);
            if (country != null)
            {
                _unitOfWork.Repository<Country>().Delete(country);
                _unitOfWork.Commit();
                return new ServiceResult(true);
            }
            return new ServiceResult(false);
        }
    }
}

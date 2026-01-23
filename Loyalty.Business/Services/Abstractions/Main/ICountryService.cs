

using Loyalty.Business.Dtos.Post.Main;
using Loyalty.Business.Results;

namespace Loyalty.Business.Services.Abstractions.Main
{
    public interface ICountryService
    {
        Task<ServiceResult> AddCountry(AddCountryDto country);
        Task<ServiceResult> GetAllCountries();
        Task<ServiceResult> GetAllCountriesWithCities();
        Task<ServiceResult> GetCountry(int id);
        Task<ServiceResult> GetCountryWithCities(int id);
        Task<ServiceResult> UpdateCountry(AddCountryDto countryDto, int id);
        Task<ServiceResult> DeleteCountry(int id);
    }
}

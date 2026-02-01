

using Loyalty.Business.Dtos.Post.Main;
using Loyalty.Business.Results;

namespace Loyalty.Business.Services.Abstractions.Main
{
    public interface ICityService
    {
        Task<ServiceResult> AddCity(AddCityDto city);
        Task<ServiceResult> GetAllCities();
        Task<ServiceResult> GetCitiesByCountry(int countryId);
        Task<ServiceResult> GetCity(int id);
        Task<ServiceResult> UpdateCity(AddCityDto cityDto, int id);
        Task<ServiceResult> DeleteCity(int id);
    }
}

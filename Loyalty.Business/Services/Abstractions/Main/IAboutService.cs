using Loyalty.Business.Dtos.Post.Main;
using Loyalty.Business.Results;

namespace Loyalty.Business.Services.Abstractions.Main
{
	public interface IAboutService
	{
        Task<ServiceResult> AddAbout(AddAboutDto aboutDto);
        Task<ServiceResult> GetAbouts();
        Task<ServiceResult> GetActiveAbouts();
        Task<ServiceResult> GetAbout(int id);
        Task<ServiceResult> GetAbout();
        Task<ServiceResult> GetLastAbout();
        Task<ServiceResult> UpdateAbout(AddAboutDto aboutDto, int id);
        Task<ServiceResult> DeleteAbout(int id);
    }
}

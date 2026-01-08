using Loyalty.Business.Dtos.Post.Main;
using Loyalty.Business.Results;

namespace Loyalty.Business.Services.Abstractions.Main
{
    public interface IOfferService
    {
        Task<ServiceResult> AddOffer(AddOfferDto offer);
        Task<ServiceResult> GetOffers();
        Task<ServiceResult> GetActiveOffers();
        Task<ServiceResult> GetOffer(int id);
        Task<ServiceResult> UpdateOffer(AddOfferDto offerDto, int id);
        Task<ServiceResult> DeleteOffer(int id);
        Task<ServiceResult> GetOffersByTenant(int tenantId);
    }
}


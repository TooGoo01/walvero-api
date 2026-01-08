using Loyalty.DataAccess.Entities.Main;
using Loyalty.DataAccess.Models;

namespace Loyalty.DataAccess.Repositories.Abstractions.Main
{
    public interface IOfferRepository : IGenericRepository<Offer>
    {
        Task<IEnumerable<Offer>> GetActiveOffers();
        Task<IEnumerable<Offer>> GetAllOffers();
        Task<Offer?> GetOffer(int offerId);
        Task<IEnumerable<Offer>> GetOffersByTenant(int tenantId);
        Task<IReadOnlyList<HomeOfferProjection>> ListHomeOffersAsync(CancellationToken ct = default);
    }
}


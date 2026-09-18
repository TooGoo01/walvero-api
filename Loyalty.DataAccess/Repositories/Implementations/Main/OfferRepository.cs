
using Loyalty.DataAccess.Context;
using Loyalty.DataAccess.Entities.Main;
using Loyalty.DataAccess.Models;
using Loyalty.DataAccess.Repositories.Abstractions.Main;
using Microsoft.EntityFrameworkCore;

namespace Loyalty.DataAccess.Repositories.Implementations.Main
{
    public class OfferRepository : GenericRepository<Offer>, IOfferRepository
    {
        public OfferRepository(LoyaltyWalletDbContext loyaltyDbContext) : base(loyaltyDbContext)
        {
        }

        public async Task<IEnumerable<Offer>> GetActiveOffers()
        {
            return await GetAsQueryable()
                .AsNoTracking()
                .Where(x => x.IsActive)
                .Include(x => x.Tenant)
                .ToListAsync();
        }

        public async Task<IEnumerable<Offer>> GetAllOffers()
        {
            return await GetAsQueryable()
                .AsNoTracking()
                .Include(x => x.Tenant)
                .ToListAsync();
        }

        public async Task<Offer?> GetOffer(int offerId)
        {
            return await GetAsQueryable()
                .AsNoTracking()
                .Include(x => x.Tenant)
                .FirstOrDefaultAsync(x => x.Id == offerId && x.IsActive);
        }

        public async Task<IEnumerable<Offer>> GetOffersByTenant(int tenantId)
        {
            return await GetAsQueryable()
                .AsNoTracking()
                .Where(x => x.TenantId == tenantId && x.IsActive)
                .Include(x => x.Tenant)
                .ToListAsync();
        }

        public async Task<IReadOnlyList<HomeOfferProjection>> ListHomeOffersAsync(CancellationToken ct = default)
        {
            var now = System.DateTime.UtcNow;
            return await _LoyaltyDbContext.Offers
                .AsNoTracking()
                .Where(o => o.IsActive)
                // Kampaniya tarixi: vaxtı bitmiş və ya hələ başlamamışları göstərmə
                .Where(o => o.ValidTo == null || o.ValidTo >= now)
                .Where(o => o.ValidFrom == null || o.ValidFrom <= now)
                .OrderByDescending(o => o.RegDate)
                .ThenByDescending(o => o.Id)
                .Select(o => new HomeOfferProjection
                {
                    Id = o.Id,
                    AzTitle = o.AzTitle,
                    EnTitle = o.EnTitle,
                    RuTitle = o.RuTitle,
                    AzDescription = o.AzDescription,
                    EnDescription = o.EnDescription,
                    RuDescription = o.RuDescription,
                    PhotoUrl = o.PhotoUrl,
                    TenantId = o.TenantId,
                    DiscountPercent = o.DiscountPercent,
                    ValidFrom = o.ValidFrom,
                    ValidTo = o.ValidTo,
                    AzTerms = o.AzTerms,
                    EnTerms = o.EnTerms,
                    RuTerms = o.RuTerms
                })
                .ToListAsync(ct);
        }

        private IQueryable<Offer> GetAsQueryable()
        {
            return _LoyaltyDbContext.Offers.AsQueryable();
        }
    }
}


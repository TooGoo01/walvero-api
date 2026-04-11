using Loyalty.DataAccess.Context;
using Loyalty.DataAccess.Entities.Main;
using Loyalty.DataAccess.Repositories.Abstractions.Main;
using Microsoft.EntityFrameworkCore;

namespace Loyalty.DataAccess.Repositories.Implementations.Main
{
    public class SalesRepVisitRepository : GenericRepository<SalesRepVisit>, ISalesRepVisitRepository
    {
        public SalesRepVisitRepository(LoyaltyWalletDbContext dbContext) : base(dbContext)
        {
        }

        public async Task<SalesRepVisit?> GetByIdAsync(int id, CancellationToken ct = default)
        {
            return await GetAsQueryable()
                .FirstOrDefaultAsync(x => x.Id == id && x.IsActive, ct);
        }

        public async Task<List<SalesRepVisit>> ListAsync(
            int? status,
            bool? remindOnly,
            string? searchText,
            CancellationToken ct = default)
        {
            var query = GetAsQueryable().Where(x => x.IsActive);

            if (status.HasValue)
            {
                var statusEnum = (SalesVisitStatus)status.Value;
                query = query.Where(x => x.Status == statusEnum);
            }

            if (remindOnly == true)
            {
                query = query.Where(x => x.RemindFlag);
            }

            if (!string.IsNullOrWhiteSpace(searchText))
            {
                var term = searchText.Trim();
                query = query.Where(x =>
                    x.VenueName.Contains(term) ||
                    (x.SalesRepName != null && x.SalesRepName.Contains(term)) ||
                    (x.VenueAddress != null && x.VenueAddress.Contains(term)) ||
                    (x.ContactName != null && x.ContactName.Contains(term)) ||
                    (x.ContactPhone != null && x.ContactPhone.Contains(term)));
            }

            return await query
                .OrderByDescending(x => x.RemindFlag)
                .ThenByDescending(x => x.VisitDate)
                .ToListAsync(ct);
        }
    }
}

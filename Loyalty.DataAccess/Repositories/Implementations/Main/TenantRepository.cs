using Loyalty.DataAccess.Context;
using Loyalty.DataAccess.Entities.Main;
using Loyalty.DataAccess.Repositories.Abstractions.Main;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Loyalty.DataAccess.Repositories.Implementations.Main
{
    public class TenantRepository : GenericRepository<Tenant>, ITenantRepository
    {
        public TenantRepository(LoyaltyWalletDbContext LoyaltyDbContext) : base(LoyaltyDbContext)
        {
        }
        public async Task<Tenant?> GetBySubdomainAsync(string subdomain, CancellationToken cancellationToken = default)
        {
            return await GetAsQueryable()
                .FirstOrDefaultAsync(t => t.Subdomain == subdomain, cancellationToken);
        }

        public async Task<bool> SubdomainExistsAsync(string subdomain, CancellationToken cancellationToken = default)
        {
            return await GetAsQueryable()
                .AnyAsync(t => t.Subdomain == subdomain, cancellationToken);
        }

        public async Task<Tenant?> GetWithBrandingAsync(int tenantId, CancellationToken cancellationToken = default)
        {
            return await GetAsQueryable()
                .Include(t => t.Branding)
                .FirstOrDefaultAsync(t => t.Id == tenantId, cancellationToken);
        }
        public async Task<List<Tenant>> GetByIdsAsync(IEnumerable<int> tenantIds, CancellationToken ct = default)
        {
            var ids = tenantIds?.Distinct().ToList() ?? new List<int>();
            if (!ids.Any())
                return new List<Tenant>();

            return await GetAsQueryable()
                .AsNoTracking()
                .Include(x=>x.Category)
                .Where(t => ids.Contains(t.Id))
                .ToListAsync(ct);
        }
        public async Task<Tenant?> GetWithAllDetailsAsync(int tenantId, CancellationToken cancellationToken = default)
        {
            return await GetAsQueryable()
                .Include(t => t.Branding)
                .Include(t => t.LoyaltyPrograms)
                .Include(t => t.WalletCredentials)
                .FirstOrDefaultAsync(t => t.Id == tenantId, cancellationToken);
        }

        public async Task<IEnumerable<Tenant>> GetAll()
        {
            return await GetAsQueryable()
              .ToListAsync();
        }

        public async Task<IEnumerable<Tenant>> GetActive()
        {
            return await GetAsQueryable().Include(x=>x.Customers).Include(x => x.Category).Where(x=>x.IsActive)
              .ToListAsync();
        }

        public async Task<Tenant?> GetById(int id)
        {
            return await GetAsQueryable().Include(x => x.Category).Include(x=>x.Admins)
               .FirstOrDefaultAsync(t => t.Id == id);
        }

        public Task<IEnumerable<Tenant>> GetByUserId(int tenantId)
        {
            throw new NotImplementedException();
        }

        public async Task<IQueryable<Tenant>> GetActiveWithUser(string? name, string? userName, int? categoryId)
        {
            var query = GetAsQueryable()
                .Include(x => x.Category)
                .Include(x => x.LoyaltyPrograms)
                    .ThenInclude(x => x.CardTemplate)
                .Where(x => x.IsActive);
            
            if (userName != null)
            {
                // Include only customers with active loyalty cards
                query = query.Include(x => x.Customers
                    .Where(c => c.PhoneNumber == userName && 
                                c.LoyaltyCards.Any(lc => lc.IsActive)))
                    .ThenInclude(c => c.LoyaltyCards.Where(lc => lc.IsActive));
            }
            else
            {
                // Include only customers with active loyalty cards (if any)
                query = query.Include(x => x.Customers
                    .Where(c => c.LoyaltyCards.Any(lc => lc.IsActive)))
                    .ThenInclude(c => c.LoyaltyCards.Where(lc => lc.IsActive));
            }
            
            if (name != null)
                query = query.Where(x => EF.Functions.Like(x.CompanyName, $"%{name}%"));
            if (categoryId != null)
                query = query.Where(x => x.categoryId == categoryId);
            
            return query.OrderBy(x => x.Id);
        }
    }
}

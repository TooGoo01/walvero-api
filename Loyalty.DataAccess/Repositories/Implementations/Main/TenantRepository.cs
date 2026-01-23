using Loyalty.DataAccess.Context;
using Loyalty.DataAccess.Entities.Main;
using Loyalty.DataAccess.Models;
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
            return await GetAsQueryable()
                .Include(x=>x.Customers)
                .Include(x => x.Category)
                .Where(x => x.IsActive && !x.IsHidden)
                .ToListAsync();
        }

        public async Task<Tenant?> GetById(int id)
        {
            return await GetAsQueryable()
               .Include(x => x.Admins)
               .Include(x => x.TenantLocation)
                   .ThenInclude(tl => tl.Country)
               .Include(x => x.TenantLocation)
                   .ThenInclude(tl => tl.City)
               .Include(x => x.TenantLocation)
                   .ThenInclude(tl => tl.TimeZone)
              .FirstOrDefaultAsync(t => t.Id == id);
        }

        public Task<IEnumerable<Tenant>> GetByUserId(int tenantId)
        {
            throw new NotImplementedException();
        }

        public IQueryable<Tenant> GetActiveWithUser(string? name, string? userName, int? categoryId, int? countryId, int? cityId)
        {
            // PERFORMANCE: Use AsNoTracking for read-only queries
            var query = GetAsQueryable()
                .AsNoTracking()
                .Include(x => x.Category)
                .Include(x => x.LoyaltyPrograms)
                    .ThenInclude(x => x.CardTemplate)
                // OPTIMIZATION: Include TenantLocation with City and Country to avoid N+1 queries
                .Include(x => x.TenantLocation)
                    .ThenInclude(tl => tl.Country)
                .Include(x => x.TenantLocation)
                    .ThenInclude(tl => tl.City)
                .Where(x => x.IsActive && !x.IsHidden);
            
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
            
            // Text search filter
            if (name != null)
                query = query.Where(x => EF.Functions.Like(x.CompanyName, $"%{name}%"));
            
            // Category filter
            if (categoryId != null)
                query = query.Where(x => x.categoryId == categoryId);
            
            // FEATURE: Country filter
            if (countryId != null)
                query = query.Where(x => x.TenantLocation != null && x.TenantLocation.countryId == countryId);
            
            // FEATURE: City filter
            if (cityId != null)
                query = query.Where(x => x.TenantLocation != null && x.TenantLocation.cityId == cityId);
            
            return query.OrderBy(x => x.Id);
        }
        public async Task<IReadOnlyList<HomeTenantProjection>> ListHomeTenantsAsync(int? countryId = null, int? cityId = null, CancellationToken ct = default)
        {
            var query = _LoyaltyDbContext.Tenants
                .AsNoTracking()
                .Where(t => t.IsActive && t.ShowOnHome && !t.IsHidden);
            
            // FEATURE: Country filter for discover/home page
            if (countryId != null)
                query = query.Where(t => t.TenantLocation != null && t.TenantLocation.countryId == countryId);
            
            // FEATURE: City filter for discover/home page
            if (cityId != null)
                query = query.Where(t => t.TenantLocation != null && t.TenantLocation.cityId == cityId);
            
            return await query
                .Select(t => new HomeTenantProjection
                {
                    TenantId = t.Id,
                    TenantName = t.CompanyName,
                    // Get first active program's name (deterministic ordering by Id)
                    TenantProgramName = t.LoyaltyPrograms
                        .Where(lp => lp.IsActive)
                        .OrderBy(lp => lp.Id)
                        .Select(lp => lp.ProgramName)
                        .FirstOrDefault(),
                    // Get GoogleLogoUrl from CardTemplate of first active program
                    GoogleLogoUrl = t.LoyaltyPrograms
                        .Where(lp => lp.IsActive && lp.CardTemplate != null)
                        .OrderBy(lp => lp.Id)
                        .Select(lp => lp.CardTemplate!.GoogleLogoUrl)
                        .FirstOrDefault(),
                    // Get RewardDescription from CardTemplate of first active program
                    // (CardTemplate.RewardDescription is the primary source for reward description)
                    RewardDescription = t.LoyaltyPrograms
                        .Where(lp => lp.IsActive && lp.CardTemplate != null)
                        .OrderBy(lp => lp.Id)
                        .Select(lp => lp.CardTemplate!.RewardDescription)
                        .FirstOrDefault()
                })
                .ToListAsync(ct);
        }

        /// <inheritdoc />
        public async Task<Tenant?> GetTenantForWalletDetailsAsync(int tenantId, CancellationToken ct = default)
        {
            return await GetAsQueryable()
                .AsNoTracking()
                .Include(t => t.Branding)
                .Include(t => t.Category)
                .Include(t => t.LoyaltyPrograms.Where(lp => lp.IsActive))
                    .ThenInclude(lp => lp.CardTemplate)
                        .ThenInclude(ct => ct!.Icons)
                .Where(t => t.Id == tenantId && t.IsActive && !t.IsHidden)
                .FirstOrDefaultAsync(ct);
        }

        /// <inheritdoc />
        public async Task<Tenant?> FindWalveroTenantAsync(CancellationToken ct = default)
        {
            return await GetAsQueryable()
                .Include(t => t.Branding)
                .Include(t => t.LoyaltyPrograms.Where(lp => lp.IsActive))
                    .ThenInclude(lp => lp.CardTemplate)
                .Where(t => t.CompanyName.ToLower() == "walvero" && t.IsHidden)
                .FirstOrDefaultAsync(ct);
        }
    }
}

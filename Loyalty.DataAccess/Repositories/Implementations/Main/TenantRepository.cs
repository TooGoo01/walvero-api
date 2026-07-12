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
              .Where(x => !x.IsDeleted)
              .ToListAsync();
        }

        public async Task<IEnumerable<Tenant>> GetActive()
        {
            return await GetAsQueryable()
                .Include(x=>x.Customers)
                .Include(x => x.Category)
                .Where(x => x.IsActive && !x.IsHidden && !x.IsDeleted)
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

        /// <inheritdoc />
        public IQueryable<Tenant> GetActiveFiltered(string? name, int? categoryId, int? countryId, int? cityId, bool excludeHiddenInApp = true)
        {
            // Lightweight query — NO Customer/Card includes.
            // isCustomer is resolved via batch query in service layer.
            var query = GetAsQueryable()
                .AsNoTracking()
                .Where(x => x.IsActive && !x.IsHidden && !x.IsDeleted);

            // HideInApp: yalnız mobil app-dan gizlədilir; web client platform=web ilə görür
            if (excludeHiddenInApp)
                query = query.Where(x => !x.HideInApp);

            // Text search filter
            if (!string.IsNullOrWhiteSpace(name))
                query = query.Where(x => EF.Functions.Like(x.CompanyName, $"%{name}%"));

            // Category filter
            if (categoryId.HasValue)
                query = query.Where(x => x.categoryId == categoryId);

            // Country filter
            if (countryId.HasValue)
                query = query.Where(x => x.TenantLocation != null && x.TenantLocation.countryId == countryId);

            // City filter
            if (cityId.HasValue)
                query = query.Where(x => x.TenantLocation != null && x.TenantLocation.cityId == cityId);

            // Admin manuel sıralama: DiscoverSortOrder təyin olunmuşlar əvvəlcə (kiçik→böyük), null olanlar sonda Id ilə.
            return query
                .OrderBy(x => x.DiscoverSortOrder == null)
                .ThenBy(x => x.DiscoverSortOrder)
                .ThenBy(x => x.Id);
        }
        public async Task<IReadOnlyList<HomeTenantProjection>> ListHomeTenantsAsync(int? countryId = null, int? cityId = null, bool excludeHiddenInApp = true, CancellationToken ct = default)
        {
            var query = _LoyaltyDbContext.Tenants
                .AsNoTracking()
                .Where(t => t.IsActive && t.ShowOnHome && !t.IsHidden && !t.IsDeleted);

            // HideInApp: yalnız mobil app-dan gizlədilir; web client platform=web ilə görür
            if (excludeHiddenInApp)
                query = query.Where(t => !t.HideInApp);

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
                    TenantSlug = t.Subdomain,
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
                    // App-specific .webp logo və strip — mobil app trending/brand list-ində istifadə edir
                    AppLogoUrl = t.LoyaltyPrograms
                        .Where(lp => lp.IsActive && lp.CardTemplate != null)
                        .OrderBy(lp => lp.Id)
                        .Select(lp => lp.CardTemplate!.AppLogoUrl)
                        .FirstOrDefault(),
                    AppStripUrl = t.LoyaltyPrograms
                        .Where(lp => lp.IsActive && lp.CardTemplate != null)
                        .OrderBy(lp => lp.Id)
                        .Select(lp => lp.CardTemplate!.AppStripUrl)
                        .FirstOrDefault(),
                    // Get RewardDescription from CardTemplate of first active program
                    // (CardTemplate.RewardDescription is the primary source for reward description)
                    RewardDescription = t.LoyaltyPrograms
                        .Where(lp => lp.IsActive && lp.CardTemplate != null)
                        .OrderBy(lp => lp.Id)
                        .Select(lp => lp.CardTemplate!.RewardDescription)
                        .FirstOrDefault(),
                    // Cashback faizləri — home brend kartında qısa "X% Cashback"
                    BronzePercent = t.LoyaltyPrograms
                        .Where(lp => lp.IsActive && lp.CardTemplate != null)
                        .OrderBy(lp => lp.Id)
                        .Select(lp => lp.CardTemplate!.BronzePercent)
                        .FirstOrDefault(),
                    BronzePercentCash = t.LoyaltyPrograms
                        .Where(lp => lp.IsActive && lp.CardTemplate != null)
                        .OrderBy(lp => lp.Id)
                        .Select(lp => lp.CardTemplate!.BronzePercentCash)
                        .FirstOrDefault()
                })
                .ToListAsync(ct);
        }

        /// <inheritdoc />
        public async Task<Tenant?> GetTenantForWalletDetailsAsync(int tenantId, CancellationToken ct = default)
        {
            // Performans qeydi:
            // Bu sorğu Tenant + Branding (1:1) + Category (1:1) + LoyaltyPrograms (1:N)
            // + CardTemplate (1:1) + Icons (1:N) yükləyir. AsSplitQuery olmadan bu Include-lar
            // tək JOIN-də Cartesian product-a çevrilir (məs. 3 program × 10 icon = 30 dublikat sətir,
            // hər biri tam Tenant + Branding + Category sütunları ilə) — yüklənmə yavaş olur.
            // AsSplitQuery EF-ə hər collection üçün ayrı SQL göndərməyi tapşırır → çox az duplicate
            // data, daha cəld serialize/parse.
            return await GetAsQueryable()
                .AsNoTracking()
                .Include(t => t.Branding)
                .Include(t => t.Category)
                .Include(t => t.LoyaltyPrograms.Where(lp => lp.IsActive))
                    .ThenInclude(lp => lp.CardTemplate)
                        .ThenInclude(ct => ct!.Icons)
                .Where(t => t.Id == tenantId && t.IsActive && !t.IsHidden)
                .AsSplitQuery()
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

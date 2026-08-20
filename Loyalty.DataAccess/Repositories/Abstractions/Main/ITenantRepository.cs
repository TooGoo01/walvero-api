using Loyalty.DataAccess.Entities.Main;
using Loyalty.DataAccess.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Loyalty.DataAccess.Repositories.Abstractions.Main
{
    public interface ITenantRepository : IGenericRepository<Tenant>
    {
        Task<Tenant?> GetBySubdomainAsync(string subdomain, CancellationToken cancellationToken = default);
        Task<Tenant?> GetById(int id);
        Task<bool> SubdomainExistsAsync(string subdomain, CancellationToken cancellationToken = default);
        Task<Tenant?> GetWithBrandingAsync(int tenantId, CancellationToken cancellationToken = default);
        Task<Tenant?> GetWithAllDetailsAsync(int tenantId, CancellationToken cancellationToken = default);
        Task<IEnumerable<Tenant>> GetAll();
        Task<IEnumerable<Tenant>> GetByUserId(int tenantId);
        Task<List<Tenant>> GetByIdsAsync(IEnumerable<int> tenantIds, CancellationToken ct = default);
        Task<List<Tenant>> GetWithBranchesByIdsAsync(IEnumerable<int> tenantIds, CancellationToken ct = default);
        Task<List<Tenant>> GetNearbyBroadcastWithBranchesAsync(CancellationToken ct = default);
        Task<IEnumerable<Tenant>> GetActive();
        /// <summary>
        /// Lightweight IQueryable for tenant discovery. Returns only active, non-hidden tenants
        /// with optional filters. No Customer/Card includes — isCustomer is resolved via batch query in service layer.
        /// </summary>
        IQueryable<Tenant> GetActiveFiltered(string? name, int? categoryId, int? countryId, int? cityId, bool excludeHiddenInApp = true, int? tenantId = null);
        Task<IReadOnlyList<HomeTenantProjection>> ListHomeTenantsAsync(int? countryId = null, int? cityId = null, bool excludeHiddenInApp = true, CancellationToken ct = default);

        /// <summary>
        /// Get tenant with branding, category, and first active loyalty program with card template.
        /// Used for wallet card details in TenantOnly mode.
        /// </summary>
        Task<Tenant?> GetTenantForWalletDetailsAsync(int tenantId, CancellationToken ct = default);

        /// <summary>
        /// Find Walvero shadow tenant by name (case-insensitive) and IsHidden flag.
        /// Used only by WalveroWalletPassService.
        /// </summary>
        Task<Tenant?> FindWalveroTenantAsync(CancellationToken ct = default);
    }
}

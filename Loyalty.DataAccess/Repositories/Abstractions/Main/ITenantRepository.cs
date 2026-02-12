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
        Task<IEnumerable<Tenant>> GetActive();
        IQueryable<Tenant> GetActiveWithUser(string? name, string? userName, int? categoryId, int? countryId, int? cityId);

        /// <summary>
        /// Discover query for an authenticated user. Filters Customers by applicationUserId and only includes active cards.
        /// This keeps Discover "IsCustomer" in sync with MyCards logic.
        /// </summary>
        IQueryable<Tenant> GetActiveWithApplicationUser(string? name, int applicationUserId, int? categoryId, int? countryId, int? cityId);
        Task<IReadOnlyList<HomeTenantProjection>> ListHomeTenantsAsync(int? countryId = null, int? cityId = null, CancellationToken ct = default);

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

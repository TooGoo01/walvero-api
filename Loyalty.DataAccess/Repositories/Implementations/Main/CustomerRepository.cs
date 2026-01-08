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
    public class CustomerRepository : GenericRepository<Customer>, ICustomerRepository
    {
        public CustomerRepository(LoyaltyWalletDbContext LoyaltyDbContext) : base(LoyaltyDbContext)
        {
        }
        public Task<Customer?> GetByEmailAsync(int tenantId, string email, CancellationToken ct = default)
          => GetAsQueryable().AsNoTracking().FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Email == email, ct);

        public async Task<Customer?> GetByPhoneAsync(int tenantId, string phone, CancellationToken ct = default)
        {
          return await  GetAsQueryable().Include(x=>x.LoyaltyCards).FirstOrDefaultAsync(x => x.TenantId == tenantId && x.PhoneNumber == phone && x.IsActive, ct);
        }

        public async Task<Customer?> GetByWalveroIdAsync(int tenantId, long walveroId, CancellationToken ct = default)
        {
            return await GetAsQueryable()
                .Include(x => x.LoyaltyCards)
                .Include(x => x.applicationUser)
                .Where(x => x.TenantId == tenantId && x.IsActive && x.applicationUser != null && x.applicationUser.WalveroId == walveroId)
                .FirstOrDefaultAsync(ct);
        }

        public async Task<List<Customer>> GetByTenant(int tenantId, int? userId)
        {
            return await GetAsQueryable().Where(x => x.IsActive && x.TenantId==tenantId).Include(x => x.LoyaltyCards)
            .ToListAsync();
        }

        public async Task<List<Customer>> GetByUserId(int userId)
        {
            return await GetAsQueryable().Include(x=>x.applicationUser).Include(x => x.LoyaltyCards).Where(x=>x.applicationUserId==userId)
            .ToListAsync();
        }

        public Task<List<Customer>> SearchAsync(int tenantId, string? q, int skip, int take, CancellationToken ct = default)
        {
            var query = GetAsQueryable().AsNoTracking().Where(x => x.TenantId == tenantId);
            if (!string.IsNullOrWhiteSpace(q))
            {
                q = q.Trim();
                query = query.Where(x =>
                    x.FirstName.Contains(q) ||
                    (x.Email != null && x.Email.Contains(q)) ||
                    (x.PhoneNumber != null && x.PhoneNumber.Contains(q)));
            }

            return query.OrderBy(x => x.FirstName).Skip(skip).Take(take).ToListAsync(ct);
        }
    }
}

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
        
        // Override GetByIdAsync to include related data for Currency mapping.
        // 3-level ThenInclude → AsSplitQuery Cartesian-i həll edir.
        // ⚠️ AsNoTracking ƏLAVƏ ETMİRİK — UserCardService.Delete kimi yerlər
        // bu metoddan qayıdan entity-i .IsActive = false ilə yeniləyir.
        public override async Task<Customer?> GetByIdAsync(int id, CancellationToken ct = default)
        {
            return await GetAsQueryable()
                .Include(x => x.LoyaltyCards.Where(c => c.IsActive))
                    .ThenInclude(x => x.LoyaltyProgram)
                        .ThenInclude(x => x.CardTemplate)
                .Include(x => x.applicationUser)
                .AsSplitQuery()
                .FirstOrDefaultAsync(x => x.Id == id, ct);
        }

        public Task<Customer?> GetByEmailAsync(int tenantId, string email, CancellationToken ct = default)
          => GetAsQueryable().AsNoTracking().FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Email == email, ct);

        // ⚠️ tracked qalır — CustomerEnrollmentService customer.IsActive = true ilə reactivate edir.
        public async Task<Customer?> GetByPhoneAsync(int tenantId, string phone, CancellationToken ct = default)
        {
            return await GetAsQueryable()
                .Include(x => x.LoyaltyCards)
                .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.PhoneNumber == phone && x.IsActive, ct);
        }

        // ⚠️ tracked qalır — PosSessionService / RedeemService update flow-larında istifadə edir.
        public async Task<Customer?> GetByWalveroIdAsync(int tenantId, long walveroId, CancellationToken ct = default)
        {
            return await GetAsQueryable()
                .Include(x => x.LoyaltyCards)
                .Include(x => x.applicationUser)
                .AsSplitQuery()
                .Where(x => x.TenantId == tenantId && x.IsActive && x.applicationUser != null && x.applicationUser.WalveroId == walveroId)
                .FirstOrDefaultAsync(ct);
        }

        public async Task<List<Customer>> GetByTenant(int tenantId, int? userId)
        {
            return await GetAsQueryable()
                .AsNoTracking()
                .Where(x => x.IsActive && x.TenantId == tenantId)
                .Include(x => x.LoyaltyCards)
                .AsSplitQuery()
                .ToListAsync();
        }

        public async Task<List<Customer>> GetByUserId(int userId)
        {
            return await GetAsQueryable()
                .AsNoTracking()
                .Include(x => x.applicationUser)
                .Include(x => x.LoyaltyCards)
                .AsSplitQuery()
                .Where(x => x.applicationUserId == userId)
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

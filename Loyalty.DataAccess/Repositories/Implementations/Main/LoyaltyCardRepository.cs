using Loyalty.DataAccess.Context;
using Loyalty.DataAccess.Entities.Main;
using Loyalty.DataAccess.Enums;
using Loyalty.DataAccess.Repositories.Abstractions.Main;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Loyalty.DataAccess.Repositories.Implementations.Main
{
    public class LoyaltyCardRepository : GenericRepository<LoyaltyCard>, ILoyaltyCardRepository
    {
        public LoyaltyCardRepository(LoyaltyWalletDbContext LoyaltyDbContext) : base(LoyaltyDbContext)
        {
        }
        public Task<LoyaltyCard?> GetByCardNumberAsync(int tenantId, string cardNumber, CancellationToken ct = default)
         => GetAsQueryable().AsNoTracking().FirstOrDefaultAsync(x => x.TenantId == tenantId && x.CardNumber == cardNumber, ct);
        public Task<LoyaltyCard?> GetByBarcodeValueAsync(int tenantId, string cardNumber, CancellationToken ct = default)
         => GetAsQueryable().Include(x=>x.Customer).ThenInclude(c => c.applicationUser).FirstOrDefaultAsync(x => x.TenantId == tenantId && x.BarcodeValue == cardNumber, ct);

        public Task<bool> ExistsAsync(int tenantId, string cardNumber, CancellationToken ct = default)
            => GetAsQueryable().AsNoTracking().AnyAsync(x => x.TenantId == tenantId && x.CardNumber == cardNumber, ct);

        public Task<List<LoyaltyCard>> ListByCustomerAsync(int tenantId, int customerId, CancellationToken ct = default)
            => GetAsQueryable().AsNoTracking()
                 .Where(x => x.TenantId == tenantId && x.CustomerId == customerId)
                 .OrderByDescending(x => x.Id)
                 .ToListAsync(ct);

        public Task<LoyaltyCard?> GetLastActiveByCustomerAsync(int tenantId, int programId, int customerId, CancellationToken ct)
        {
            return GetAsQueryable()
                .Where(x => x.TenantId == tenantId && 
                           x.CustomerId == customerId && 
                           x.LoyaltyProgramId == programId &&
                           x.IsActive)
                .OrderByDescending(x => x.Id)
                .FirstOrDefaultAsync(ct);
        }

        public Task<LoyaltyCard?> GetById(int id, CancellationToken ct)
        {
            return GetAsQueryable().Include(x=>x.Customer).ThenInclude(x=>x.applicationUser).FirstOrDefaultAsync(x => x.Id==id, ct);
        }

        public async Task<List<LoyaltyCard>> GetLastActiveCardsForApplicationUserAsync(
      int applicationUserId,
      string? name,
      int? categoryId,
      CancellationToken ct = default)
        {
            // 1) Əsas sorğu – user-in aktiv kartları
            var baseQuery = GetAsQueryable()
                .AsNoTracking()
                .Where(c =>
                    c.IsActive &&
                    c.Customer != null &&
                    c.Customer.applicationUserId == applicationUserId &&
                    c.Customer.IsActive &&
                    c.Status == CardStatus.Active);

            // 1.1) Mağaza adına görə filter (Tenant.CompanyName)
            if (!string.IsNullOrWhiteSpace(name))
            {
                baseQuery = baseQuery.Where(c =>
                    EF.Functions.Like(c.Tenant.CompanyName, $"%{name}%"));
                // əgər Tenant navigation yoxdursa, belə et:
                // baseQuery = baseQuery.Where(c =>
                //     EF.Functions.Like(c.LoyaltyProgram.Tenant.CompanyName, $"%{name}%"));
            }

            // 1.2) Kateqoriyaya görə filter
            if (categoryId.HasValue)
            {
                baseQuery = baseQuery.Where(c => c.Tenant.categoryId == categoryId.Value);
                // yenə də Tenant yoxdursa:
                // baseQuery = baseQuery.Where(c => c.LoyaltyProgram.Tenant.categoryId == categoryId.Value);
            }

            // 2) Hər (TenantId, LoyaltyProgramId) üçün son kartın Id-sini tap
            var lastCardIdsQuery =
                from card in baseQuery
                group card by new { card.TenantId, card.LoyaltyProgramId } into g
                select g
                    .OrderByDescending(x => x.Id) // CreatedAt varsa onu istifadə et
                    .First().Id;

            var lastCardIds = await lastCardIdsQuery.ToListAsync(ct);

            if (!lastCardIds.Any())
                return new List<LoyaltyCard>();

            // 3) İndi həmin kartları full include ilə götür (Customer, Program və s.)
            var cards = await GetAsQueryable()
                .AsNoTracking()
                .Where(c => lastCardIds.Contains(c.Id))
                .Include(c => c.Customer)
                .Include(c => c.LoyaltyProgram)
                    .ThenInclude(p => p.CardTemplate)
                .Include(c => c.Tenant)
                .ToListAsync(ct);

            return cards;
        }

    }
}
